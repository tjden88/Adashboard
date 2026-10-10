# Проксировать health-check карточек и получение заголовка страницы через Adashboard.Api

## Цель
Проверка доступности карточек (`IHealthCheckService`) и получение заголовка страницы (`IPageTitleService`)
в WASM-фронтенде не работают: браузер делает запросы напрямую к целевым сервисам, а те не отдают
CORS-заголовки, поэтому `fetch` отклоняется и карточки всегда показываются как `Offline`.
Нужно перенести сами сетевые запросы на сервер (`Adashboard.Api`), где CORS не действует, а WASM-клиент
должен обращаться к своему API. Тогда health-check и заголовок будут работать для любых домашних
сервисов, а индикатор статуса сможет показывать понятное пояснение (в подсказке при наведении).

## Требования

### Рабочая область
- Изменения только в `Adashboard.Api` (серверный прокси) и `Adashboard` (WASM-клиент).
- `Adashboard.Web` (SvelteKit) не трогаем. `Adashboard.Domain` не изменяем.

### Разделение контрактов: health-check по `id`, заголовок по `url`
- **Health-check принимает только `id` карточки.** Для периодических проверок карточка уже существует
  в БД (в `Home.razor` перед проверкой вызывается сохранение раскладки), поэтому API сам находит карточку,
  читает `UrlScheme`/`Url`/`StatusMode` и решает, куда и зачем идти.
- **Заголовок принимает `url`.** Диалог карточки запрашивает заголовок для ещё не сохранённой карточки
  (`CardDialog.FetchTitle`), когда `id` в БД отсутствует.

### Серверный прокси в Adashboard.Api
- Добавить сервис `Adashboard.Api/Services/ProbeService.cs`: интерфейс `IProbeService` и реализация.
  - `Task<CardHealthResult> CheckCardAsync(int cardId, CancellationToken ct)` — находит карточку в БД
    через `IDbContextFactory<DashboardDbContext>` (`Cards`, `AsNoTracking`), собирает абсолютный
    `http(s)`-URL из `UrlScheme + Url`, выполняет запрос и **сам определяет итоговый статус** по
    `StatusMode` карточки, возвращая статус и текстовое сообщение.
  - `Task<string?> GetTitleAsync(string url, CancellationToken ct)` — загружает HTML и возвращает
    содержимое тега `<title>` или `null`, если тега нет.
  - Использовать `IHttpClientFactory` с именованным клиентом и `AllowAutoRedirect = false`
    (как у клиента загрузки иконок): редиректы не следуем, чтобы код ответа был настоящим.
  - Таймауты: статус — 5 секунд, заголовок — 8 секунд (как сейчас в WASM-сервисах).
  - Проверка статуса использует `HttpCompletionOption.ResponseHeadersRead` и не читает тело.
  - Получение заголовка: чтение тела с лимитом 256 КБ, декодирование по charset из ответа (по умолчанию
    UTF-8), извлечение `<title>` регулярным выражением, нормализация пробелов, ограничение 120 символов —
    логика переносится из текущего `Adashboard/Services/PageTitleService.cs` без изменения поведения.
  - Логика определения статуса переносится из клиентского `Evaluate` без изменения семантики:
    `Success` → `online` при 2xx; `NoServerError` → `online` при коде < 500; `Reachable` → `online` при
    любом ответе; `None` или некорректный адрес → `unknown`.
- Добавить контроллер `Adashboard.Api/Controllers/ProbeController.cs` с маршрутом `api/probe`:
  - `GET /api/probe/health/{cardId}` → `200` `{ "status": "online|offline|unknown", "message": "..." }`;
    `404` `{ "error": "..." }`, если карточка не найдена; `400` при `cardId <= 0`.
  - `GET /api/probe/title?url={абсолютный URL}` → `200` `{ "title": <string|null> }`; `400` `{ "error": "..." }`,
    если `url` не является абсолютным `http`/`https`.
  - Оформление — по образцу `CardsController`: `[ApiController]`, `[ProducesResponseType]`,
    возврат анонимных объектов.
- Результат health-check — DTO с полями `status` и `message` (record в проекте API, например
  `CardHealthResult`). `message` — короткое понятное пояснение на русском: тип исхода, без хостов,
  URL и чувствительных деталей. Примеры:
  - online: `Доступен: ответ 200.` / `Сервис отвечает.`;
  - offline: `Сервер вернул ошибку 503.` / `Превышено время ожидания (5 с).` /
    `Не удалось подключиться к сервису.` / `Не удалось разрешить адрес сервиса.`;
  - unknown: `Проверка статуса отключена.` / `Некорректный адрес карточки.`.
- Зарегистрировать `IProbeService` и именованный `HttpClient` в `Adashboard.Api/Program.cs`.
- Логирование: через `ILogger` в `ProbeService` и контроллере на русском языке по правилам проекта —
  лог начала операции, лог успешного завершения, `LogWarning` для нетипичных сценариев (некорректный URL,
  карточка не найдена, таймаут, сетевая ошибка). В логи можно писать детали (в т.ч. адрес), но не секреты.

### Решение по SSRF (зафиксировать в комментариях)
- Фильтрация диапазонов адресов (частные/локальные/служебные IP) намеренно **не применяется**:
  карточки ведут на домашние сервисы, то есть это сознательный отказ от защиты, обратный логике
  `ImageIconService.EnsurePublicHostAsync`. Обосновать это решением для личного дашборда в XML-комментарии.
- Оставшиеся ограничения: только схемы `http`/`https`, без следования редиректам, ограничение таймаута
  и размера тела. В сообщении для UI не показывать внутренние адреса и детали ошибок.

### WASM-клиент: единый Refit-клиент
- Проект уже переведён на Refit (`IDashboardApi`). Добавить в него методы (и клиентские DTO ответов):
  - `[Get("/api/probe/health/{cardId}")] Task<CardHealthResponse> CheckHealthAsync(int cardId, CancellationToken ct = default);`
  - `[Get("/api/probe/title")] Task<CardTitleResponse> GetTitleAsync([Query] string url, CancellationToken ct = default);`
  - `CardHealthResponse(string Status, string? Message)` и `CardTitleResponse(string? Title)` — небольшие
    record-типы рядом с `IDashboardApi`. DTO не выносим в `Adashboard.Domain` (иначе пришлось бы тянуть
    туда `ServiceHealthState`); небольшое дублирование формы ответа допустимо.
  - `Status` — строка (`online`/`offline`/`unknown`), клиент переводит её в `ServiceHealthState`.

### WASM-клиент: health-check (`Adashboard/Services/HealthCheckService.cs`)
- Оставить enum `ServiceHealthState` (нужен для отображения). Добавить результат проверки с сообщением:
  `ServiceHealthResult(ServiceHealthState State, string? Message)`.
- `IHealthCheckService.CheckAsync(...)` меняет контракт: принимает `int cardId` (а не карточку) и
  возвращает `Task<ServiceHealthResult>`.
- Реализация становится тонкой обёрткой над `IDashboardApi.CheckHealthAsync`: вызывает API, переводит
  строковый `Status` в `ServiceHealthState`, прокидывает `Message`.
- Убрать из WASM сборку URL, `Evaluate` и всю работу с HTTP к целевым сервисам.
- При ошибке вызова самого API (`ApiException`, `HttpRequestException`) вернуть
  `ServiceHealthResult(Unknown, "Не удалось проверить статус сервиса.")`; отмену пробрасывать, как сейчас.

### WASM-клиент: заголовок страницы (`Adashboard/Services/PageTitleService.cs`)
- Превратить сервис в тонкую обёртку над `IDashboardApi.GetTitleAsync(url)`.
- Разбор HTML, декодирование, лимиты и сборка `Uri` переносятся на сервер; из WASM-сервиса удаляются
  regex и работа с HTML.
- Сохранить контракт `TryGetTitleAsync(string url, CancellationToken)`: вернуть заголовок либо выбросить
  `InvalidOperationException` с понятным русским сообщением (некорректная ссылка, ошибка сервиса, таймаут).

### Отображение сообщения на индикаторе (UI)
- `Home.razor`: словарь `_health` хранит `ServiceHealthResult` вместо `ServiceHealthState`;
  `GetHealthState`/`CheckCardNowAsync`/`CheckAllCardsAsync` обновить под новый тип и `card.Id`.
- `Category.razor`: `HealthLookup` меняет тип на `Func<DashboardCard, ServiceHealthResult>`.
- `Card.razor`: параметр статуса — `ServiceHealthResult` (значение по умолчанию — `Unknown` без сообщения);
  у индикатора `title` показывает `Message`, а если его нет — прежнюю подпись (`Сервис доступен` и т.п.),
  `aria-label` остаётся человекочитаемой подписью состояния.
- Убрать регистрацию внешнего `HttpClient` в `Adashboard/Program.cs` (оба сервиса теперь идут через
  `IDashboardApi`); регистрации `IHealthCheckService`/`IPageTitleService` сохранить.

### Стиль и комментарии
- В WASM-проекте `Adashboard` — подробные обучающие комментарии на русском языке (правило проекта
  для фронтенда).
- В `Adashboard.Api` — XML-комментарии у сервиса, всех публичных методов и свойств, плюс комментарии
  у нетривиальной логики; логирование по правилам проекта.
- Не добавлять новые зависимости и лишние абстракции.

## Критерии приёмки
- [x] В `Adashboard.Api` есть `ProbeService` и `ProbeController` с эндпоинтами
      `GET api/probe/health/{cardId}` и `GET api/probe/title?url=...`, зарегистрированные в DI/`Program.cs`.
- [x] `GET /api/probe/health/{cardId}` сам находит карточку в БД, выполняет запрос к её сервису
      (в т.ч. без CORS) и возвращает `{ status, message }`; для несуществующего `id` — `404`.
- [x] `GET /api/probe/title?url=...` возвращает заголовок страницы целевого сервиса без CORS.
- [x] Некорректный или не-`http(s)` URL даёт `400` с понятным сообщением; интерфейс не падает.
- [x] `HealthCheckService` в WASM обращается только к API через `IDashboardApi`; прямых запросов из браузера
      к целевым сервисам не осталось, JS-ошибок CORS больше нет.
- [x] В живом дашборде карточки сервисов без CORS показывают `Online`/`Offline` корректно;
      недоступный сервис или таймаут → `Offline` (не `Unknown`).
- [x] При наведении на индикатор статуса показывается сообщение из ответа API; сообщение не содержит
      внутренних адресов и деталей ошибок, при пустом сообщении остаётся прежняя подпись.
- [x] `PageTitleService` получает заголовок через API; для сервиса без CORS получение работает,
      при ошибке — понятное сообщение.
- [x] Внешний `HttpClient` для прямых запросов удалён из `Adashboard/Program.cs`; новые зависимости не добавлены.
- [x] `dotnet build Adashboard.slnx` проходит без ошибок и предупреждений.
- [x] Проект `Adashboard.Web` не изменён.
