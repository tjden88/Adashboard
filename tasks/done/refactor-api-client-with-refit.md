# Внедрить Refit для доступа WASM-клиента к Adashboard.Api

## Цель
Сейчас WASM-фронтенд обращается к своему API вручную: сервисы `DashboardLayoutHttpService`
и `ImageIconHttpService` сами собирают `HttpClient`-запросы, разбирают ответы и повторяют
одну и ту же обвязку. Задача — описать контракт API декларативно через библиотеку **Refit**,
чтобы клиентский код стал типизированным и короче, а взаимодействие с API — единообразным.

После задачи обращения к Adashboard.Api (раскладка и изображения карточек) идут через один
Refit-интерфейс; вспомогательные HTTP-сервисы-обёртки удалены, а компоненты используют
Refit-клиент напрямую. Поведение dashboard (загрузка/сохранение раскладки, загрузка/удаление
иконки карточки) сохраняется. Внешние запросы (`HealthCheckService`, `PageTitleService`)
в рамках этой задачи **не трогаем**.

## Требования

### Область работ
- Изменения только во фронтенде `Adashboard` (WASM).
- `Adashboard.Api`, `Adashboard.Domain` и `Adashboard.Web` не изменяем.
- `HealthCheckService`, `PageTitleService`, `DashboardApiOptions`, `DialogService`,
  `EditModeService` не трогаем. `HealthCheckService` и `PageTitleService` по-прежнему
  ходят на произвольные абсолютные URL напрямую и остаются в текущем виде.

### Подключение Refit
- Добавить в `Adashboard/Adashboard.csproj` пакеты `Refit` и `Refit.HttpClientFactory`
  (последние стабильные версии, совместимые с `net10.0`). Больше зависимостей не добавлять.
- В `Adashboard/Program.cs` зарегистрировать Refit-клиент с базовым адресом API
  (тот же `apiBaseUrl`, что используется сейчас):
  ```csharp
  builder.Services
      .AddRefitClient<IDashboardApi>()
      .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseUrl));
  ```
- Существующую регистрацию сырого `HttpClient` (`builder.Services.AddScoped(_ => new HttpClient ...)`)
  **оставить** — она нужна `HealthCheckService` и `PageTitleService`.
- Удалить регистрации `IDashboardLayoutService` и `IImageIconService`.

### Refit-интерфейс `IDashboardApi`
- Новый файл `Adashboard/Services/IDashboardApi.cs` — единственный Refit-интерфейс к своему API,
  описывающий существующие эндпоинты (по образцу `Adashboard.Api/Controllers`):
  - `GET /api/layout` → `Task<DashboardLayout> GetLayoutAsync(CancellationToken)`.
  - `PUT /api/layout` (`[Body] DashboardLayout`) → сохранение.
  - `POST /api/cards/{cardId}/image` (multipart/form-data, поле `file`) → `Task<HttpResponseMessage>`.
  - `POST /api/cards/{cardId}/image/from-url` (`[Body]` с полем `url`) → `Task<HttpResponseMessage>`.
  - `DELETE /api/cards/{cardId}/image` → удаление.
- Для multipart использовать идиому Refit `[Multipart]` + `[AliasAs("file")] StreamPart`
  (создаётся из `byte[]` через `MemoryStream`), сохранив имя поля `file` и content-type
  `application/octet-stream`, как сейчас.
- Тело запроса `from-url` передавать анонимным объектом `new { url }` (без нового DTO), так как
  новых моделей по решению задачи не вводим.
- Для эндпоинтов изображений возвращать `HttpResponseMessage` и разбирать ответ вручную —
  сохранить текущий минимальный разбор `imagePath`/`error` (см. ниже), новых DTO не добавлять.
- Использовать `DashboardLayout` из `Adashboard.Domain` для layout напрямую (это доменная модель,
  а не новый DTO).
- Константу лимита размера изображения перенести на интерфейс: `IDashboardApi.MaxImageBytes`
  (сейчас `IImageIconService.MaxImageBytes`, значение `2 * 1024 * 1024`).
- Оформить подробными обучающими XML-комментариями на русском языке: что такое Refit, что делают
  атрибуты `[Get]`/`[Put]`/`[Post]`/`[Delete]`/`[Body]`/`[Multipart]`/`[AliasAs]`, как путь
  склеивается с `BaseAddress`.

### Компоненты используют Refit напрямую
- `Adashboard/Components/Pages/Home.razor`:
  - Заменить `@inject IDashboardLayoutService` и `@inject IImageIconService` на `@inject IDashboardApi DashboardApi`.
  - `LoadLayoutAsync`: `_layout = await DashboardApi.GetLayoutAsync() ?? new DashboardLayout { Id = 1, ThemeMode = "auto" };`
    (сохранить прежний запасной вариант, когда API вернуло пустую раскладку).
  - `SaveLayoutAsync`: вызывать `DashboardApi.SaveLayoutAsync(_layout)`; при неуспехе Refit бросает
    `ApiException` — сохранить понятное сообщение об ошибке (обернуть в `InvalidOperationException`
    с кодом ответа, как делал прежний сервис).
  - Удаление изображений карточки/категории (`DeleteCard`, `DeleteCategory`): заменить
    `ImageIconService.Delete(...)` на `await DashboardApi.DeleteImageAsync(...)`, обернув вызов
    в `try/catch` с `Logger.LogWarning` — удаление не должно срывать сохранение раскладки
    (прежний `Delete` был fire-and-forget и не пробрасывал ошибки).
- `Adashboard/Components/Pages/CardDialog.razor`:
  - Заменить `@inject IImageIconService` на `@inject IDashboardApi DashboardApi`; добавить
    `@inject ILogger<CardDialog> Logger` для логирования ошибок удаления.
  - `IImageIconService.MaxImageBytes` → `IDashboardApi.MaxImageBytes` (в `OnFileSelected`,
    `OnClipboardImage`).
  - Сохранение изображения из байтов: собрать `StreamPart` и вызвать
    `DashboardApi.UploadImageAsync(cardId, part)`; из успешного ответа прочитать `imagePath`.
  - Сохранение изображения по ссылке: `DashboardApi.UploadImageFromUrlAsync(cardId, new { url = _imageUrl.Trim() })`;
    из успешного ответа прочитать `imagePath`.
  - Перенести в приватные методы `CardDialog` разбор ответа из прежнего `ImageIconHttpService`:
    при неуспехе бросить `InvalidOperationException` с полем `error` из тела (или сообщением
    по умолчанию), при успехе вернуть `imagePath`; если `imagePath` пуст — понятная ошибка.
    Сохранить существующую обработку исключений в `Save`.
  - Удаление при возврате к встроенной иконке: `await DashboardApi.DeleteImageAsync(cardId)`
    в `try/catch` с `LogWarning`.

### Удаление старых сервисов
- Удалить файлы: `Services/IDashboardLayoutService.cs`, `Services/IImageIconService.cs`,
  `Services/DashboardLayoutHttpService.cs`, `Services/ImageIconHttpService.cs`.
- Убедиться, что на них не осталось ссылок.

### Trim / AOT-совместимость
- Проверить сборку/публикацию в Release (в проекте включены `PublishTrimmed=true`
  и `<RunAOTCompilation>false</RunAOTCompilation>`).
- Если обрезка ломает сериализацию Refit/System.Text.Json — настроить Refit через
  `RefitSettings` с source-generated `JsonSerializerContext`; ограничиться минимальной
  настройкой, без лишних абстракций.
- Если Release-проверку выполнить нельзя, явно сообщить об этом в отчёте и не считать
  критерий выполненным.

### Стиль и комментарии
- В `Adashboard` — подробные обучающие комментарии на русском языке (правило проекта для фронтенда).
- Не добавлять новые DTO, абстракции и зависимости помимо Refit.

## Критерии приёмки
- [ ] В `Adashboard.csproj` подключены `Refit` и `Refit.HttpClientFactory`; других новых зависимостей нет.
- [ ] Создан `Services/IDashboardApi.cs` с эндпоинтами `GET/PUT /api/layout` и
      `POST/POST(from-url)/DELETE /api/cards/{cardId}/image`; Refit-клиент зарегистрирован в `Program.cs`.
- [ ] Файлы `IDashboardLayoutService.cs`, `IImageIconService.cs`, `DashboardLayoutHttpService.cs`,
      `ImageIconHttpService.cs` удалены, ссылок на них не осталось.
- [ ] `Home.razor` и `CardDialog.razor` используют `IDashboardApi` напрямую.
- [ ] Загрузка dashboard, сохранение раскладки, сохранение изображения (из файла и по ссылке)
      и удаление изображения работают как раньше; ошибки удаления не ломают сохранение раскладки.
- [ ] Лимит размера изображения (`2 МБ`) и его проверки сохранены (теперь через `IDashboardApi.MaxImageBytes`).
- [ ] `HealthCheckService` и `PageTitleService` не изменены и продолжают использоваться как прежде.
- [ ] `dotnet build Adashboard.slnx` проходит без ошибок и предупреждений.
- [ ] Release-сборка/публикация `Adashboard` выполняется и проверена на работоспособность
      (либо явно описано, почему проверка не выполнялась).
- [ ] Проекты `Adashboard.Api`, `Adashboard.Domain` и `Adashboard.Web` не изменены.
