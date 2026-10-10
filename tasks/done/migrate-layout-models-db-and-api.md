# Перенос моделей, БД и API раскладки из Blazor в Adashboard.Api

## Цель
Начать перенос backend-функциональности из легаси Blazor-проекта `Adashboard` в целевой ASP.NET Web API (`Adashboard.Api`). На этом этапе нужно перенести доменные модели раскладки, слой хранения данных (EF Core + SQLite с миграциями и стартовым наполнением) и реализовать REST-эндпоинты чтения и полного сохранения основной раскладки dashboard. Остальную функциональность (health-check, получение заголовка страницы, загрузка иконок, статика загрузок) на этом этапе не переносим.

## Требования
- Перенести доменные модели, необходимые для раскладки, в `Adashboard.Api` с namespace `Adashboard.Api.Models.Dashboard`:
  - `DashboardLayout`, `DashboardCategory`, `DashboardCard`, `CategoryPosition`, `CardPosition`, `CardStatusMode`.
  - Переносить как отдельные копии; проект `Adashboard` не изменяем, поэтому его модели остаются на месте.
- Перенести слой данных в `Adashboard.Api` (namespace `Adashboard.Api.Data`), обновив пространства имён:
  - `DashboardDbContext`;
  - миграцию вместе с `.Designer.cs` и `DashboardDbContextModelSnapshot.cs` (namespace `Adashboard.Api.Data.Migrations`); схема таблиц и идентификатор/ProductVersion миграции должны остаться прежними, чтобы совместимо работать с существующей базой;
  - `DashboardDatabaseBootstrap`, `DebugDbInitializer`, `ProductionDbInitializer`;
  - `DashboardStorage` и `IDashboardLayoutService` + `DashboardLayoutService`.
- Добавить в `Adashboard.Api.csproj` пакеты:
  - `Microsoft.EntityFrameworkCore.Sqlite` (версия как в Blazor — `10.0.12`);
  - `Microsoft.EntityFrameworkCore.Design` (версия `10.0.12`, `PrivateAssets="all"`).
- Сохранить соглашения хранения данных из Blazor:
  - единый корневой каталог данных из переменной окружения `DASHBOARD_DATA_PATH`, файл `adashboard.db`;
  - `DashboardStorage` переносим без изменения поведения (оба пути — база и `uploads`; каталог `uploads` пока не используется, сохраняется как общее соглашение каталога данных);
  - применение миграций и стартовое наполнение при запуске: демо-раскладка в Development (`DebugDbInitializer`), стартовая раскладка в Production (`ProductionDbInitializer`).
- Настроить `Program.cs` в `Adashboard.Api`:
  - регистрация `IDbContextFactory<DashboardDbContext>` с SQLite по пути из `DashboardStorage`;
  - `AddSingleton(storage)`, `AddScoped<IDashboardLayoutService, DashboardLayoutService>`;
  - сохранить существующую настройку логирования (единый формат консоли и фильтр уровня по `LOG_LEVEL` для логов приложения);
  - применить инициализацию БД при старте до `app.Run()`;
  - сохранить подключённый OpenAPI и `MapControllers()`.
- Реализовать контроллер раскладки в `Adashboard.Api.Controllers`:
  - `GET /api/layout` — возвращает основную раскладку (id = 1), как доменную модель; при отсутствии в базе — раскладку по умолчанию (как `GetMainLayoutAsync`);
  - `PUT /api/layout` — полная замена основной раскладки (как `SaveMainLayoutAsync`); при `null` в теле — `400 Bad Request`, при успехе — `204 No Content`.
- Все комментарии в новом коде (включая XML-комментарии) писать на русском языке.
- Логи сервисов на русском, без секретов, со структурированными параметрами.
- Не настраивать CORS и не раздавать каталог `uploads` статикой на этом этапе.
- Не изменять проект `Adashboard`: он должен продолжать собираться и работать как раньше.

## Критерии приёмки
- [ ] В `Adashboard.Api` присутствуют перенесённые доменные модели в namespace `Adashboard.Api.Models.Dashboard`.
- [ ] В `Adashboard.Api` присутствуют `DashboardDbContext`, миграция и инициализаторы БД; схема таблиц совпадает со схемой Blazor-проекта.
- [ ] `dotnet build Adashboard.slnx` собирает решение без ошибок.
- [ ] `GET /api/layout` возвращает основную раскладку (после старта база заполняется стартовыми данными).
- [ ] `PUT /api/layout` полностью сохраняет переданную раскладку, и повторный `GET /api/layout` возвращает сохранённые данные.
- [ ] Проект `Adashboard` (Blazor) не изменён и продолжает собираться.
- [ ] Все новые комментарии и XML-комментарии — на русском языке.
