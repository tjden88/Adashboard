---
name: extension
description: Use when working in Adashboard.Extension (Manifest V3 browser extension, popup, options page).
---

# Расширение браузера (Adashboard.Extension)

## Стеки и границы

- TypeScript + Vite, Manifest V3 для Chrome, Edge, Firefox. Отдельный npm-проект, в `Adashboard.slnx` не входит.
- UI-фреймворков и runtime-зависимостей нет: только devDependencies (`typescript`, `vite`, `@types/chrome`).
- Проверки: `npm run typecheck`, `npm run build`.
- Интерфейс: `popup.html` и `options.html`; background/service worker нет.
- Кроссплатформенный доступ к API браузера — `src/browser.ts` (`globalThis.browser ?? globalThis.chrome`), полифилл не добавляем.
- Разрешения: `storage`, `activeTab`, `scripting`. Host-разрешений нет: CORS сервера разрешает любой origin.
- `browser_specific_settings.gecko.id` обязателен для Firefox; `strict_min_version` — `129.0`
  (код использует `AbortSignal.timeout` и `AbortSignal.any`).

## Настройки и API

- Адрес сервера и `ExtensionApiKey` — только в `storage.local`, не в `sync`.
- `ApiKeySettings.IsEnabled` задаёт основной `ApiKey`; проверка принимает любой из двух ключей.
- Серверная часть — только существующие эндпоинты API (`/api/pages`, `/api/pages/{id}`, `/api/categories`,
  `/api/cards`, `/api/cards/icon`, `/api/health/access`). Новых эндпоинтов под расширение не заводим.
- Ошибки API показываются текстом из тела ответа (`text/plain`), как это делает клиент.
- Отказ необязательных шагов (иконка, `theme-color`) ошибку не показывает: используется значение по умолчанию.

## Категории и карточки

- Категорию «Закладки» расширение создаёт только при сохранении карточки, не при открытии попапа.
- Порядок категории — следующий после максимального на странице (как `PageView.NextCategoryOrder`),
  ширина — 6, `mobileWidth` — `null`.
- Запомненные страница и категория (для каждой страницы своя) живут в `storage.local`.

## Состояния попапа и Vite

- Состояния: загрузка, пустой результат, ошибка, повтор неудачного шага.
- Обработчики общих кнопок («Настройки», «Повторить») навешиваются до проверки настроек и вкладки —
  иначе кнопка видима, но без обработчика. Отказ `runtime.openOptionsPage` показываем в попапе.
- `base: './'`, `modulePreload.polyfill: false` (CSP `extension_pages` в MV3); `public/manifest.json`
  копируется в `dist` как есть, пути внутри манифеста — относительно `dist`.
- CSP страниц задаётся в манифесте явно (`script-src 'self'; object-src 'self'`), без
  `upgrade-insecure-requests`: политика по умолчанию в Firefox поднимает `http`-запросы к `https`,
  и расширение не подключится к серверу по `http` в локальной сети.
- Иконки — PNG 16/32/48/128 в `public/icons`; зависимости для этого не добавляем.
- Постоянная установка ограничена браузерами: в Chrome unpacked-папка живёт, пока включён режим
  разработчика; в Firefox — только подписанный `.xpi`.
- `dist/` и `node_modules/` в репозиторий не попадают; `package.json` и `package-lock.json` сохраняем.
