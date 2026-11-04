---
name: blazor-ui
description: Use when working on Adashboard.Client components, dialogs, widgets, state, or client services.
---

# Blazor WebAssembly (Adashboard.Client)

## Hosting-модель

Standalone WASM, статику отдаёт `Adashboard.Server` (один origin с API). Не переключать render mode.
Ссылки на статику и загрузки — относительные пути. Базовый адрес API — `builder.HostEnvironment.BaseAddress`
(`Program.cs`), его же использует `DashboardApiOptions.ResolveUploadPath`.

## Штатные механизмы вместо самодельных

- **Общее состояние** — корневые каскадные значения: `AddCascadingValue` с `CascadingValueSource<T>`
  (`isFixed: false`) и `NotifyChangedAsync`. В проекте так: `AppStateService` (режим редактирования,
  настройки) и `AuthStateService` (доступ). Состояние гранулярное: одна «большая» модель на всё приложение не подходит.
- **Режим и оформление** — класс на корневом элементе + CSS (`dashboard-edit-mode`), без JS и без перерисовки каждого компонента.
- **Навигация** — `NavigationManager`, `@page`, `NavLink`; переходы не перезагружают приложение.
- **Заголовок вкладки и `<head>`** — `PageTitle`, `HeadOutlet`.
- **Формы** — `EditForm`, `DataAnnotationsValidator`, `ValidationMessage`.
- **Списки и диалоги** — `@key`, `DynamicComponent`, `Virtualize`.
- **Ошибки отрисовки** — `ErrorBoundary`.
- **Жизненный цикл и ресурсы** — `OnInitializedAsync`, `OnParametersSetAsync`, `OnAfterRenderAsync`,
  `IAsyncDisposable`, `CancellationTokenSource`; освобождать подписки, таймеры, interop.
- **JS interop** — только там, где Blazor и CSS не справляются (drag-and-drop, ColorPicker).

## Требования к компонентам

- Отдельный компонент на логически выделенную часть UI; файлы небольшие.
- Параметры и `EventCallback`, а не прямой доступ к состоянию дочерних компонентов.
- Асинхронные виджеты: явно обрабатывать загрузку, пустой результат, ошибку и повторную попытку.
- Ошибка одного виджета не ломает весь dashboard.
- Семантический HTML, клавиатурная навигация, заметный focus, понятные подписи; работа на desktop, tablet, mobile.
- Не допускать лишних перерисовок и повторных сетевых запросов.

## Предметная область

- Конфигурация dashboard — пользовательские данные, а не код: проверять на границе приложения,
  показывать понятные сообщения.
- Стабильные идентификаторы элементов, не отображаемые имена и индексы списков.
- Новые свойства конфигурации не должны терять сохранённые настройки.
- Для периодических проверок: интервалы, таймауты, повторы, кэширование, параллельные health-check.
- В UI не показывать секреты, внутренние URL, stack trace, чувствительные детали health-check.
