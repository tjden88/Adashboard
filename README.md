# Adashboard

Персональная стартовая страница (dashboard) с настраиваемыми категориями, ссылками и
индикаторами доступности сервисов. По концепции похожа на Dashy и Homarr.

## Возможности

- Категории и карточки-ссылки с настраиваемым порядком и шириной в сетке (до 12 колонок).
- Режим редактирования: добавление, изменение и удаление категорий и карточек.
- Перетаскивание категорий и карточек (на Pointer Events, без сторонних библиотек).
- Иконки: Font Awesome, загрузка изображения с устройства или по ссылке (с проверкой формата и защитой от SSRF).
- Индикаторы доступности сервисов: режимы проверки `None`, `Success`, `NoServerError`, `Reachable`.
- Автоматическое определение заголовка страницы по адресу карточки.
- Оформление: темы `auto`/`light`/`dark`, заголовок и описание сайта, логотип, фоны.
- Периодическое обновление статусов с настраиваемым интервалом.

## Технологии

- .NET 10, Blazor Web App (Interactive Server).
- Entity Framework Core + SQLite.
- Bootstrap, Font Awesome, Coloris.

## Запуск локально

Требуется .NET SDK 10.

```powershell
dotnet run --project Adashboard
```

Приложение откроется на `http://localhost:5115`. В режиме Development база автоматически
создаётся и наполняется демонстрационными данными.

## Запуск в Docker

```powershell
docker compose up -d --build
```

Приложение будет доступно на `http://localhost:8080`. Данные хранятся в именованном
volume `adashboard-data`.

Без compose:

```powershell
docker build -t adashboard .
docker run -d -p 8080:8080 -v adashboard-data:/data -e LOG_LEVEL=Information --name adashboard adashboard
```

## Конфигурация

| Переменная | По умолчанию | Назначение |
| --- | --- | --- |
| `DASHBOARD_DATA_PATH` | каталог проекта | Единый каталог данных: `adashboard.db` и `uploads`. В Docker — `/data`. |
| `LOG_LEVEL` | `Warning` | Уровень логов сервисов приложения (`Trace`…`None`). Логи Microsoft/EF настраиваются в `appsettings.json`. |
| `ASPNETCORE_HTTP_PORTS` | `8080` (в образе) | Порт прослушивания контейнера. |
| `ASPNETCORE_ENVIRONMENT` | `Production` (в образе) | Окружение хостинга. |

## Хранение данных

Всё изменяемое состояние лежит в одном каталоге (`DASHBOARD_DATA_PATH`):

```
/data
├── adashboard.db        # база данных SQLite (миграции применяются при старте)
├── adashboard.db-wal
├── adashboard.db-shm
└── uploads
    ├── backgrounds      # поставляемые фоны и логотип
    └── card-icons       # пользовательские иконки карточек
```

При первом запуске стандартные фоны и логотип копируются в каталог загрузок, поэтому
пустой volume не теряет оформление. Для бэкапа достаточно скопировать весь каталог данных.

## Развёртывание

- **Один экземпляр.** SQLite не поддерживает конкурентную запись с нескольких реплик —
  не масштабируйте приложение горизонтально.
- **Reverse proxy.** Blazor Server использует SignalR поверх WebSocket: прокси должен
  пропускать `Upgrade`/`Connection` и иметь увеличенный таймаут. При терминации TLS на
  прокси задайте `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.
- **Права на volume.** Контейнер работает от непривилегированного пользователя `app`
  (uid 1654). При bind-mount владелец каталога должен совпадать.
- **HTTPS.** Наружу отдавайте приложение только через HTTPS-прокси.

## Лицензия

Проект распространяется под лицензией [GNU General Public License v3.0](LICENSE) or later.
