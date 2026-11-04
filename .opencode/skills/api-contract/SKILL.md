---
name: api-contract
description: Use when changing controllers, endpoints, the Refit client, OpenAPI output, or HTTP error responses.
---

# API-контракт

## Клиент не пишется руками

`IDashboardApi` генерируется на сборке по `openapi/Adashboard.Server.json` (файл в репозитории).
Конфиг: `Adashboard.Client/dashboard-api.refitter` (`generateContracts: false`, `returnIApiResponse: true`,
`useCancellationTokens: true`, `operationNameTemplate: "{operationName}Async"`). Выход —
`Adashboard.Client/Generated/IDashboardApi.g.cs` (gitignore, руками не правится).

- Имя метода клиента = `operationId`, который берётся из `Name = "..."` в атрибуте HTTP-метода контроллера.
  Добавляя эндпоинт, задавай `Name`, а не правишь клиент.
- Тип ответа в контроллере обязан быть указан через `[ProducesResponseType<T>]`, иначе в клиент попадёт
  метод без результата.
- После изменения API — две сборки: первая обновляет спецификацию, вторая пересобирает клиент.

## Контракт ошибок

- Отказ — 400 или 404 по смыслу, тело — `text/plain` с понятным текстом, без JSON-обёрток `{ error }`.
- Клиент читает тело как текст через `ApiResponseExtensions.ErrorText` и показывает как есть.
- Автоматический 400 от валидации модели приводится к тому же виду (`InvalidModelStateResponseFactory`
  в `Adashboard.Server/Program.cs`, текст «Некорректный запрос.»).
- Проверки, выполнимые только до сервиса (пустое тело, невалидный путь страницы), остаются в контроллере.
- Успешный ответ загрузки изображения-иконки — текст (путь к файлу).

## Проверка

Имена методов и коды ответов смотреть выборкой из `openapi/Adashboard.Server.json` по `operationId`,
а не чтением файла целиком (40 КБ).
