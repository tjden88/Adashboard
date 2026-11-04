---
name: api-contract
description: Use when changing controllers, endpoints, the Refit client, OpenAPI output, or HTTP error responses.
---

# API-контракт

## Клиент не пишется руками

Клиентские интерфейсы генерирует локальный dotnet-tool `GenerateAspNetCoreClient.Refit` (манифест
`dotnet-tools.json`): он собирает и поднимает `Adashboard.Server`, читает метаданные эндпоинтов (ApiExplorer)
и пишет по одному интерфейсу на контроллер — `Adashboard.Client/Generated/I<controller>Client.cs`
(namespace `Adashboard.Client.Services`, файлы в git, руками не правятся).

- Запуск после изменения API: `generate-clients.bat` (он же `scripts/generate-api-clients.ps1`), затем сборка решения.
- OpenAPI-файл в репозитории не нужен: `/openapi/v1.json` сервер отдаёт только в среде разработки для Scalar.
- Имя метода клиента = имя **действия контроллера**, суффикса `Async` нет: `CardsController.CreateCard` →
  `ICardsClient.CreateCard`. Поэтому действия называют говоряще (`GetPages`, `SaveSettings`), а `Name` в
  HTTP-атрибутах не задаём — маршрут задаётся только атрибутом (`[HttpGet("title")]`).
- Тип ответа в контроллере обязан быть указан через `[ProducesResponseType<T>]`, иначе в клиент попадёт
  метод без результата. Генератор при этом печатает «Cannot find return type» — для действий без тела ответа
  (`IActionResult`) это ожидаемое сообщение, клиент получает `Task<IApiResponse>`.
- Новый контроллер: перегенерировать клиентов и добавить интерфейс в `ApiClientRegistration.AddDashboardApiClients`
  (там используется `AddRefitGeneratedClient`: обычный `AddRefitClient` требует reflection-построитель запросов,
  которого в WASM-клиенте нет).
- Генератор исполняет `Program.cs` сервера, поэтому скрипт ставит `Adashboard:SkipStartupInitialization=true`:
  миграции и стартовое наполнение при генерации не выполняются.

## Контракт ошибок

- Отказ — 400 или 404 по смыслу, тело — `text/plain` с понятным текстом, без JSON-обёрток `{ error }`.
- Клиент читает тело как текст через `ApiResponseExtensions.ErrorText` и показывает как есть.
- Автоматический 400 от валидации модели приводится к тому же виду (`InvalidModelStateResponseFactory`
  в `Adashboard.Server/Program.cs`, текст «Некорректный запрос.»).
- Проверки, выполнимые только до сервиса (пустое тело, невалидный путь страницы), остаются в контроллере.
- Успешный ответ загрузки изображения-иконки — текст (путь к файлу).

## Проверка

Имена методов клиента и коды ответов смотреть в `Adashboard.Client/Generated/*.cs` (по одному файлу на
контроллер) или в атрибутах контроллера — не читать весь `Adashboard.Client/Generated` каталог целиком.
