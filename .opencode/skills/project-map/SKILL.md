---
name: project-map
description: Use at the start of any task to locate code cheaply — search the symbol index instead of listing or reading files. Also regenerates the index after structural changes.
---

# Project map

Индекс `docs/project-map.md` — по одной короткой строке на файл: путь и ключевые символы
(типы, публичные методы, маршруты Razor, `@inject`, экспорты TypeScript). Он нужен, чтобы определить,
**какой файл открыть**, не читая проект.

## Порядок работы

1. Ищи имя символа в `docs/project-map.md` (`grep`), а не перечисляй файлы.
2. Открывай только файл из найденной строки и только нужный участок (10–20 строк вокруг символа).
3. Если индекса нет или он устарел — regenerate:
   `powershell -File scripts/project-map.ps1`
   Параметры: `-Projects Adashboard.Server`, `-MaxSymbols 3`, `-Full`, `-Stdout`, `-Budget 8000`.
4. Индекс не вставлять в ответ и не читать целиком: в контекст берёт найденную строку.

## Что индекс не показывает

- Сигнатуры Refit-методов клиента — смотреть `openapi/Adashboard.Server.json` по `operationId`.
- Строки разметки Razor и CSS — индекс содержит только код-символы.
- Файлы разметки агентов (`AGENTS.md`, `.opencode/skills/**`) — в репозитории и доступны поиску.

## Бюджет

Весь индекс ~12 КБ (~4k токенов) — это норма для файла поиска, но не для контекста: постоянно в контексте
должно быть только ядро `AGENTS.md` и выбранный скилл. Держать файл под 13 КБ; если растёт — уменьшать
`-MaxSymbols`, а не читать его целиком.
