# Перевести Blazor на Blazor WebAssembly и связать с API через общий Adashboard.Domain

## Цель

Превратить легаси-проект `Adashboard` (Blazor Server с прямой работой с БД) в клиентское
приложение Blazor WebAssembly, которое читает и сохраняет данные через `Adashboard.Api`.
Общие типы, используемые и API, и клиентом, вынести в новый проект `Adashboard.Domain`.

Договорённости по границам задачи:

- `Adashboard` и `Adashboard.Api` ходят в один API; SvelteKit-фронтенд `Adashboard.Web` не трогаем.
- WASM-клиент разворачивается отдельно (standalone), API включает CORS.
- В API выносим только раскладку (уже есть) и операции с иконками карточек.
  Health-check и получение заголовка страницы остаются в клиенте и могут не работать
  из-за CORS — это допустимо на данном этапе.
- Имя проекта `Adashboard` сохраняем.
- Меняем только код проектов: Dockerfile, compose.yml, README не трогаем.

## Требования

- Создать проект `Adashboard.Domain` (classlib, `net10.0`, nullable reference types) и добавить
  его в `Adashboard.slnx`. Пространство имён — плоское `Adashboard.Domain`.
- В `Adashboard.Domain` перенести общие типы:
  - сущности Dashboard: `DashboardLayout`, `DashboardCategory`, `DashboardCard`,
    `CardPosition`, `CategoryPosition`, `CardStatusMode` (включая вспомогательные методы
    `DashboardCard.ShouldUseLightText`/`GetIconColor`/`GetTextColor`);
  - `SiteSettings`;
  - `FontAwesomeIcons`.
- `Adashboard.Api` и `Adashboard` должны ссылаться на `Adashboard.Domain`; удалить
  дублирующиеся модели из `Adashboard.Api/Models` и `Adashboard/Models`.
- Обновить ссылки на пространство имён сущностей в EF-снимке и Designer-файле миграции API
  (`DashboardDbContextModelSnapshot.cs`, `20260925011711_InitialCreate.Designer.cs`) с
  `Adashboard.Api.Models.Dashboard` на `Adashboard.Domain`. Схема БД не меняется,
  новая миграция не создаётся.
- API: добавить эндпоинты работы с иконкой карточки, перенеся логику из клиентского
  `ImageIconService` (проверка сигнатуры формата, ограничение размера 2 МБ, защита от SSRF,
  имя файла с хэшем содержимого, удаление старых файлов карточки):
  - `POST /api/cards/{cardId}/image` — загрузка файла (multipart/form-data, поле `file`),
    ответ `200 { "imagePath": "/uploads/card-icons/..." }`;
  - `POST /api/cards/{cardId}/image/from-url` — тело `{ "url": "https://..." }`,
    ответ `200 { "imagePath": "/uploads/card-icons/..." }`;
  - `DELETE /api/cards/{cardId}/image` — ответ `204`.
  - Некорректный ввод (не HTTP(S)-ссылка, неподдерживаемый формат, файл больше 2 МБ,
    некорректный `cardId`) возвращает `400` с понятным сообщением.
- API: раздавать статические файлы `/uploads` (`backgrounds`, `card-icons`) и при старте
  копировать стандартные фон/логотип из `wwwroot`, если их нет (перенести
  `DashboardUploadsInitializer` из Blazor-проекта).
- API: включить CORS для origin WASM-клиента; адрес(а) origin задавать через конфигурацию,
  в Development по умолчанию разрешить локальный origin клиента.
- `Adashboard`: перевести `.csproj` на `Microsoft.NET.Sdk.BlazorWebAssembly`, добавить
  пакеты WebAssembly и DevServer, убрать серверные пакеты/настройки EF Core и SQLite.
- `Adashboard`: убрать серверный код из `Program.cs` (`AddInteractiveServerComponents`,
  render mode Server, antiforgery, `MapStaticAssets`/`Assets`, `ResourcePreloader`,
  `ImportMap`, `BlazorDisableThrowNavigationException`) и компоненты/разметку,
  специфичные для Blazor Server (`ReconnectModal` и т.п.).
- `Adashboard`: точка входа — `wwwroot/index.html` (хост-документ с `<div id="app">` и
  `_framework/blazor.webassembly.js`) и `Program.cs` на `WebAssemblyHostBuilder` с корневым
  компонентом-маршрутизатором.
- `Adashboard`: заменить серверный `IDashboardLayoutService` (+ `DashboardLayoutService` из БД)
  на HTTP-клиент, вызывающий `/api/layout` (GET/PUT). Базовый адрес API брать из конфигурации
  клиента (`wwwroot/appsettings.json`, например `ApiBaseUrl`). JSON API — camelCase; enum
  `CardStatusMode` сериализуется как число (совместимо с текущим форматом API).
- `Adashboard`: заменить `IImageIconService` на HTTP-реализацию, вызывающую новые эндпоинты
  API. Сохранить сигнатуры `SaveFromUrlAsync`, `SaveFromBytesAsync`, `Delete` и константу
  `MaxImageBytes`, чтобы `CardDialog.razor` и `Home.razor` не требовали изменения логики.
- `Adashboard`: health-check и получение заголовка оставить в клиенте, переписав их с
  `IHttpClientFactory` на `HttpClient`; гарантия работы из-за CORS не требуется.
- `Adashboard`: удалить всю работу с БД (`Data/*`, `DashboardDbContext`, миграции,
  `DashboardStorage`, инициализаторы БД, SQLite-настройки и файл `adashboard.db`).
- Сохранить текущий UI и поведение dashboard: те же компоненты, CSS, JS (`wwwroot`),
  темы, режим редактирования, drag-and-drop.
- Не изменять проект `Adashboard.Web`.

## Критерии приёмки

- [ ] `dotnet build` всего решения (`Adashboard.slnx`) проходит без ошибок.
- [ ] В решении есть `Adashboard.Domain`; на него ссылаются `Adashboard.Api` и `Adashboard`;
      дубли моделей удалены из обоих проектов.
- [ ] `Adashboard` собирается как Blazor WebAssembly: в сборке есть `_framework/blazor.webassembly.js`,
      и в проекте нет ссылок на EF Core / SQLite / ASP.NET Core hosting.
- [ ] В Blazor-проекте не осталось кода и пакетов для работы с БД.
- [ ] При запуске API и WASM раскладка загружается через `GET /api/layout` и сохраняется
      через `PUT /api/layout` (проверяется вручную в браузере).
- [ ] Загрузка иконки из файла и по URL, а также удаление иконки работают через API;
      сохранённое изображение открывается по пути `/uploads/card-icons/...`.
- [ ] Логотип и стандартные фоны отдаются API по `/uploads/backgrounds/...`.
- [ ] CORS разрешает запросы WASM-клиента к API (проверяется вручную).
- [ ] Проект `Adashboard.Web` (SvelteKit) не изменён.
