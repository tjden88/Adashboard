"""Локальная раздача опубликованного Blazor WebAssembly-клиента Adashboard.

Зачем отдельный сервер, а не стандартный `python -m http.server`:

* в публикации рядом с файлами лежат предсжатые копии `.br` (Brotli) и `.gz`
  (Gzip). Обычный http.server их игнорирует, и браузер скачивает несжатые файлы;
* нужен корректный MIME `application/wasm`, иначе Blazor не использует
  потоковую компиляцию WebAssembly;
* нужны правильные заголовки кэширования, иначе при каждом обновлении страницы
  приложение скачивается заново целиком.

Кэширование (включено по умолчанию):

* файлы с хешем в имени (`_framework/System.Private.CoreLib.<hash>.wasm` и т.п.)
  отдаются как `Cache-Control: public, max-age=31536000, immutable` — браузер
  после первой загрузки берёт их из кэша вообще без запроса;
* остальные файлы (`index.html`, `appsettings.json`, `app.css`, шрифты, иконки)
  отдаются как `Cache-Control: no-cache`: браузер хранит их, но перед
  использованием проверяет актуальность. Сервер поддерживает условные запросы
  (`If-Modified-Since`) и отвечает `304 Not Modified`, поэтому повторное
  открытие страницы почти ничего не скачивает.

Флаг `--no-cache` отключает кэш (`Cache-Control: no-store`) — нужен только для
честного замера размера первой загрузки в DevTools.

Использование:
    python scripts/serve-publish.py [путь_к_публикации] [порт] [--no-cache]

Путь по умолчанию — вывод публикации из командной строки:
    Adashboard/bin/Release/net10.0/browser-wasm/publish/wwwroot
Указывать нужно каталог, в котором лежит index.html (обычно `.../publish/wwwroot`).
Порт по умолчанию — 5173.
"""

from __future__ import annotations

import functools
import http.server
import mimetypes
import os
import re
import socketserver
import sys
from email.utils import parsedate_to_datetime

# Дополняем таблицу MIME типами, которые важны для Blazor и могут отсутствовать
# в стандартном наборе Python.
for _ext, _mime in {
    ".wasm": "application/wasm",
    ".dat": "application/octet-stream",
    ".webmanifest": "application/manifest+json",
    ".json": "application/json",
    ".js": "text/javascript",
    ".mjs": "text/javascript",
    ".css": "text/css",
    ".svg": "image/svg+xml",
    ".ico": "image/x-icon",
    ".woff": "font/woff",
    ".woff2": "font/woff2",
    ".ttf": "font/ttf",
}.items():
    mimetypes.add_type(_mime, _ext)

# Порядок важен: Brotli сжимает сильнее, поэтому проверяем его первым.
ENCODINGS = (("br", ".br"), ("gzip", ".gz"))

# Файлы с хешем содержимого в имени (Blazor добавляет 10 base36-символов перед
# расширением). Такие имена меняются при любой правке, поэтому их можно кэшировать
# «навсегда».
HASHED_ASSET = re.compile(r"\.[0-9a-z]{10}\.(wasm|js|dat|blat)$", re.IGNORECASE)

IMMUTABLE_CACHE = "public, max-age=31536000, immutable"
REVALIDATE_CACHE = "no-cache"

DEFAULT_PORT = 5173
DEFAULT_PUBLISH_DIR = os.path.join(
    "Adashboard", "bin", "Release", "net10.0", "browser-wasm", "publish", "wwwroot"
)


class PrecompressedHandler(http.server.SimpleHTTPRequestHandler):
    """Обработчик с предсжатыми `.br`/`.gz`, условными запросами и кэш-заголовками."""

    # Значение по умолчанию для ответов, которые не проходят через send_head
    # (например, 404). Для остальных запросов проставляется в send_head.
    _cache_control = REVALIDATE_CACHE
    disable_cache = False

    def end_headers(self) -> None:
        cache_control = "no-store" if self.disable_cache else self._cache_control
        self.send_header("Cache-Control", cache_control)
        super().end_headers()

    def send_head(self):
        path = self.translate_path(self.path)
        self._cache_control = self._resolve_cache_control(path)

        # Для каталогов не меняем поведение: пусть SimpleHTTPRequestHandler
        # сгенерирует список файлов.
        if not os.path.isdir(path):
            compressed, encoding = self._find_precompressed(path)
            if compressed is not None:
                return self._send_precompressed(compressed, path, encoding)

        return super().send_head()

    def _resolve_cache_control(self, path: str) -> str:
        relative = os.path.relpath(path, self.directory or ".").replace(os.sep, "/")
        return IMMUTABLE_CACHE if HASHED_ASSET.search(relative) else REVALIDATE_CACHE

    def _find_precompressed(self, path: str) -> tuple[str | None, str | None]:
        accept_encoding = self.headers.get("Accept-Encoding", "") or ""
        for encoding, extension in ENCODINGS:
            compressed = path + extension
            if encoding in accept_encoding and os.path.isfile(compressed):
                return compressed, encoding
        return None, None

    def _is_not_modified(self, mtime: float) -> bool:
        header = self.headers.get("If-Modified-Since")
        if not header:
            return False
        try:
            since = parsedate_to_datetime(header).timestamp()
        except (TypeError, ValueError):
            return False
        return int(mtime) <= int(since)

    def _send_precompressed(self, compressed: str, original: str, encoding: str):
        try:
            handle = open(compressed, "rb")
        except OSError:
            return super().send_head()

        try:
            if self._is_not_modified(os.stat(original).st_mtime):
                handle.close()
                # Отдаём те же валидаторы, что и при полном ответе, чтобы клиент
                # корректно обновил запись в кэше.
                self.send_response(304)
                self.send_header("Last-Modified", self.date_time_string(os.stat(original).st_mtime))
                self.end_headers()
                return None

            stat = os.fstat(handle.fileno())
            self.send_response(200)
            # Content-Type берём от исходного файла, а не от сжатой копии.
            self.send_header("Content-Type", self.guess_type(original))
            self.send_header("Content-Encoding", encoding)
            self.send_header("Content-Length", str(stat.st_size))
            self.send_header("Last-Modified", self.date_time_string(os.stat(original).st_mtime))
            self.send_header("Vary", "Accept-Encoding")
            self.end_headers()
        except Exception:
            handle.close()
            raise

        return handle


def main() -> int:
    arguments = list(sys.argv[1:])
    disable_cache = "--no-cache" in arguments
    if disable_cache:
        arguments.remove("--no-cache")

    publish_dir = arguments[0] if arguments else DEFAULT_PUBLISH_DIR
    port = int(arguments[1]) if len(arguments) > 1 else DEFAULT_PORT

    # Путь может быть указан относительно каталога запуска или корня репозитория.
    candidates = [publish_dir]
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    candidates.append(os.path.join(repo_root, publish_dir))

    for candidate in candidates:
        root = os.path.abspath(candidate)
        if os.path.isfile(os.path.join(root, "index.html")):
            break
    else:
        print(f"Не найден index.html в {os.path.abspath(publish_dir)}.")
        print("Сначала опубликуйте клиент: dotnet publish Adashboard/Adashboard.csproj -c Release -o publish")
        return 1

    PrecompressedHandler.disable_cache = disable_cache

    socketserver.ThreadingTCPServer.allow_reuse_address = True
    handler = functools.partial(PrecompressedHandler, directory=root)
    with socketserver.ThreadingTCPServer(("127.0.0.1", port), handler) as httpd:
        mode = "без кэша (для замеров)" if disable_cache else "с кэшированием"
        print(f"Раздаю {root} [{mode}]")
        print(f"Откройте http://localhost:{port}/ (Ctrl+C для остановки)")
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nОстановлено.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
