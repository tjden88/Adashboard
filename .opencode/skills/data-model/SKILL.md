---
name: data-model
description: Use when changing EF Core entities, DashboardDb, migrations, DbInitializer, or domain contract models.
---

# Данные и модели

## Границы

- `Adashboard.Data` — только хранение: `DashboardDb`, `*Entity`, миграции, `DbInitializer`.
  Сущности наружу не отдаются: сервисы и контроллеры работают с контрактными моделями из `Adashboard.Domain`.
- Контракты и справочники — `Adashboard.Domain`: `Contracts/`, `ReadModels/`, `Enums/`, `Reference/`
  (`DomainConstants`, `FontAwesomeIcons`, `PagePathRules`).
- Маппинг сущность ↔ контракт — `Adashboard.Server/Mapping/MapperConfig.cs` (Mapster, без кодогенерации).

## Страницы (Page)

- Домен «Страница» — отдельный dashboard со своим набором категорий и карточек; старое название «раскладка» не используется.
- `DashboardPage` (`Id`, `Title`, `Description`, `Path`), агрегатная read-модель `DashboardPageReadModel`
  (`Page`, `Categories`, `Cards`), сущность `DashboardPageEntity` (`DbSet Pages`).
- `Path` — относительный путь: латиница в нижнем регистре, цифры, дефисы; уникален, максимум 120 символов,
  slug генерирует `PagePathRules`.
- Основная страница — пустой путь, открывается по `/`, не удаляется и не меняет путь. Остальные — `/{Path}`.
- Категория привязана через `DashboardPageId` и может быть перенесена на другую страницу.

## Миграции и данные

- Миграции создаёт и применяет пользователь по явному запросу; при запуске `DbInitializer` применяет существующие.
- Каталог данных и загрузок — `DashboardPaths`; SQLite, `appdata/` в gitignore.
- Ограничения (длины полей, размер изображения, интервалы) — `DomainConstants`, использовать из атрибутов
  домена, сущностей, API и клиента.
- URL, подписи, иконки и настройки виджетов — недоверенные данные: проверять схемы URL, не отображать
  непроверенный HTML, учитывать SSRF при обращениях к пользовательским URL.
- Авторизация проверяется на сервере; ключ доступа (`ApiKey`, `ExtensionApiKey`) — в конфигурации или
  пользовательских секретах, не в репозитории.
