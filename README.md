# Adashboard

Настраиваемая стартовая страница для личного использования, по концепции похожая на Dashy или Homarr.

Возможности:

- несколько страниц dashboard, у каждой — свой набор категорий и карточек;
- карточки-ссылки с иконками (Font Awesome или загруженное изображение), цветом фона и размером;
- индикатор состояния сервиса (health-check) и заголовок целевой страницы;
- настройки оформления: тема, интервал проверки состояния, вид бокового меню;
- защита API ключом доступа;
- хранение данных в SQLite — внешних зависимостей (отдельной базы, кэша) не требуется.

## Структура решения

| Проект | Назначение |
| --- | --- |
| `Adashboard.Client` | Blazor WebAssembly (.NET 10) — фронтенд. В развёртывании его статику отдаёт сервер. |
| `Adashboard.Server` | ASP.NET Web API (.NET 10) — REST API, пользовательские файлы и хостинг статики клиента. |
| `Adashboard.Domain` | контрактные модели, enum'ы и справочники, общие для клиента и сервера. |
| `Adashboard.Data` | слой хранения на EF Core: контекст, сущности БД, миграции, инициализация. |

## Требования

- .NET SDK 10 — для локального запуска и сборки.
- Docker и Docker Compose — для развёртывания в контейнере.

## Локальный запуск

```bash
dotnet build
dotnet run --project Adashboard.Server
```

Приложение доступно на `http://localhost:5024` (адрес задаётся в `Adashboard.Server/Properties/launchSettings.json`).

OpenAPI-документ и интерактивная документация Scalar доступны только в среде `Development`:
`http://localhost:5024/openapi/v1.json` и `http://localhost:5024/scalar`.

Каталог данных при локальном запуске — `Adashboard.Server/appdata`.

## Настройки

Конфигурация читается стандартным механизмом ASP.NET Core: `appsettings.json`, пользовательские
секреты (`dotnet user-secrets set "ApiKey" "ключ"`) или переменные окружения. В Docker настройки
задаются через `environment` в `docker-compose.yml`.

| Параметр | По умолчанию | Назначение |
| --- | --- | --- |
| `ApiKey` | пусто | ключ доступа к API; пустое значение отключает защиту (см. раздел «Ключ доступа») |
| `Logging__LogLevel__Default` | `Information` | уровень логирования. Для читаемых в Docker логов достаточно `Information` |
| `ASPNETCORE_URLS` | `http://localhost:5024` при локальном запуске, `http://+:8080` в образе | адреса, на которых слушает сервер |

## Хранение данных

Все изменяемые данные лежат в каталоге `appdata`:

- `appdata/data.db` — база SQLite;
- `appdata/uploads` — пользовательские загрузки: иконки карточек, фоны, стандартные ассеты
  (`/uploads/assets/bg-dark.jpg`, `bg-light.jpg`, `icon.png`).

Миграции и стартовое наполнение (стартовая страница, категории, карточки, настройки по умолчанию)
применяются автоматически при запуске приложения. В контейнере `appdata` — volume, поэтому данные
переживают пересоздание контейнера.

## Запуск в Docker

Сборка образа:

```bash
docker build -t adashboard .
```

Запуск без compose — volume с данными создаётся автоматически:

```bash
docker run -d --name adashboard -p 8080:8080 -v adashboard-data:/app/appdata adashboard
```

Запуск через compose (файл `docker-compose.yml` в корне репозитория):

```bash
docker compose up -d --build
```

Dashboard доступен на `http://localhost:8080`, проверка API — `http://localhost:8080/api/health`.

Просмотр логов и остановка:

```bash
docker logs -f adashboard
docker compose down          # или docker stop adashboard
```

## Обновление

```bash
git pull
docker compose up -d --build
```

Данные в volume сохраняются, миграции применяются автоматически при старте контейнера.

## Резервная копия и восстановление

Резервную копию volume с данными делает временный контейнер `alpine`:

```bash
# backup — архив в текущий каталог
docker run --rm -v adashboard-data:/data -v "$PWD:/backup" alpine tar -czf /backup/adashboard-data.tar.gz -C /data .

# restore — восстановление из архива
docker run --rm -v adashboard-data:/data -v "$PWD:/backup" alpine tar -xzf /backup/adashboard-data.tar.gz -C /data
```

После восстановления контейнер нужно перезапустить. Не удаляйте volume без необходимости:
`docker compose down -v` удаляет volume `adashboard-data` вместе с базой и загрузками, следующий
запуск создаёт БД и стартовое наполнение заново.

## Ключ доступа

`ApiKey` — значение конфигурации или переменная окружения:

```bash
docker run -d --name adashboard -p 8080:8080 -v adashboard-data:/app/appdata \
  -e ApiKey="ваш-ключ" -e ASPNETCORE_ENVIRONMENT=Production adashboard
```

- При пустом значении защита API отключена — весь API доступен без ключа.
- При заданном значении каждый запрос к API должен приходить с заголовком `X-Api-Key`, иначе
  сервер отвечает `401`. Клиент в этом случае показывает экран ввода ключа; ключ хранится в браузере.
- Статика клиента и пользовательские загрузки остаются открытыми: без них dashboard не загрузился бы.

Не храните секрет в репозитории: используйте переменную окружения, пользовательские секреты
(`dotnet user-secrets`) или `.env`, который не попадает в Git.

## Reverse proxy и HTTPS

Если приложение работает за reverse proxy (nginx, Caddy, Traefik), завершите HTTPS на proxy и
проксируйте все запросы на контейнер (порт `8080`) с заголовками `X-Forwarded-Host` и
`X-Forwarded-Proto`. Отдельных настроек приложения для этого не требуется: клиент использует адрес
документа как базовый адрес API, поэтому dashboard и API остаются на одном origin.

## Лицензия

GPL-3.0 — см. файл `LICENSE`.
