# Разделение моделей раскладки: плоские контракты записи и вложенная read-модель

## Цель

Убрать вложенные дочерние коллекции из контрактных моделей и разделить направления чтения и записи:
запись (`POST`/`PUT`) принимают плоские модели на сущность, а чтение агрегата отдаёт отдельная вложенная read-модель.
Так уходит главный источник двусмысленности, когда одна и та же модель служила и ответом `GET`, и телом `POST`/`PUT`,
и несла игнорируемые навигационные коллекции. Это задел под будущие несколько раскладок: read-модель легко
отдавать по запрошенной раскладке, а точечные мутации остаются простыми.

Фронтенд `Adashboard.Client` в этой задаче **не трогаем** — он в процессе переработки и может не собираться.
Схема БД и `Adashboard.Data` не меняются, миграции не создаются.

## Требования

### Контрактные модели (`Adashboard.Domain.Models`)

- Переименовать `DashboardLayoutInfo` в `DashboardLayout`. Модель несёт `Id`, `Title`, `Description`
  (добавить `Id` для единообразия с `DashboardCategory`/`DashboardCard` и чтобы read-модель отдавала идентификатор).
  Сохранить атрибуты валидации (`[Required]`, `[MaxLength]`). Модель остаётся телом `PUT /api/layout/info`.
- Удалить прежний `DashboardLayout` с `Categories`: раскладка больше не содержит дочерних коллекций.
- `DashboardCategory`: убрать свойство `Cards`. Остаются `Id`, `DashboardLayoutId`, `Title`, `Order`, `Width`.
- `DashboardCard`: без изменений (коллекций уже нет); UI-хелперы `GetIconColor()`/`GetTextColor()` остаются на модели.
- Добавить `DashboardLayoutReadModel` — read-модель раскладки:
  `DashboardLayout Layout` и `IReadOnlyList<DashboardCategoryReadModel> Categories`.
- Добавить `DashboardCategoryReadModel` — узел read-модели:
  `DashboardCategory Category` и `IReadOnlyList<DashboardCard> Cards`.
- Вложенность в read-модели строим переиспользованием плоских моделей (без дублирования их полей).
  Коллекции в read-моделях — `IReadOnlyList<...>`.
- Каждый публичный тип и свойство — с XML-комментариями на русском; отметки, что плоские модели —
  это контракты записи, а read-модель — агрегат для чтения.

### Маппинг (`Adashboard.Server.Mapping`, Mapperly)

- Убрать маппинги полного графа раскладки (`ToModel(DashboardLayoutEntity)`/`ToEntity(DashboardLayout)` в прежнем виде).
- Категория: убрать `MapperIgnoreSource/Target` для `Cards`; маппинги `ToModel`/`ToEntity`/`UpdateCategory` сохранить
  для плоской модели.
- Карточка: маппинги без изменений.
- Вложенную read-модель собираем в сервисе (LINQ); отдельный маппинг Mapperly для неё не обязателен.
  При необходимости допустим маппинг `DashboardLayoutEntity → DashboardLayout` с игнором `Categories`.
- Ручного маппинга не писать — только Mapperly.

### Сервис `LayoutService`

- `GetMainLayoutAsync` возвращает `DashboardLayoutReadModel`: поля раскладки — из сущности; категории упорядочены
  по `Order`; карточки внутри категории — по `Position`; сборка узлов — LINQ в сервисе (без загрузки графа в модель).
- `UpdateInfoAsync` принимает `DashboardLayout`, меняет `Title`/`Description` раскладки `Id = 1`; `Id` во входной модели
  игнорируется. Поведение и ответы не меняются.
- CRUD категорий и карточек — без изменений поведения (плоские модели).
- Сохранить XML-комментарии и логирование (русский язык, структурированные параметры, начало/успех/`LogWarning`).

### Контроллеры

- `LayoutController.Get` — `ActionResult<DashboardLayoutReadModel>`, `[ProducesResponseType<DashboardLayoutReadModel>]`.
- `LayoutController.PutInfo` — тело `DashboardLayout?`.
- `PUT /api/layout` отсутствует (полного сохранения графа нет).
- `CategoriesController`/`CardsController` — без изменений контрактов (плоские модели).

### Прочее

- Комментарии в коде — только на русском.
- Фронтенд `Adashboard.Client` не изменять; проект `Adashboard.Data` не изменять.
- Миграции не создавать и не применять.

## Критерии приёмки

- [ ] `dotnet build Adashboard.Domain` и `dotnet build Adashboard.Server` проходят без ошибок.
- [ ] В `Adashboard.Domain.Models`: есть `DashboardLayout` (`Id`/`Title`/`Description`), `DashboardLayoutReadModel`,
      `DashboardCategoryReadModel`; тип `DashboardLayoutInfo` отсутствует; `DashboardCategory` без `Cards`.
- [ ] `GET /api/layout` возвращает вложенную read-модель: `layout` (`id`/`title`/`description`) и `categories`
      (каждая — плоская категория + её карточки), с корректным порядком категорий и карточек.
- [ ] `PUT /api/layout/info` принимает `DashboardLayout` и изменяет заголовок/описание; `PUT /api/layout` отсутствует.
- [ ] `POST`/`PUT`/`DELETE` по `/api/categories` и `/api/cards` работают; модели запроса и ответа — плоские, без `Cards`.
- [ ] Маппинг entity ↔ контрактная модель — через Mapperly (source generator); ручного маппинга в коде нет.
- [ ] Все новые комментарии и сообщения логов — на русском языке.
