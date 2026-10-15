// Оформление dashboard: режим темы, фоновые изображения страницы, раскладка категорий,
// а также перетаскивание категорий и карточек.
//
// Модуль оформления подключает MainLayout, раскладку и перетаскивание — PageView.

let activeThemeMode = "auto";
let systemThemeMedia = null;

// Режим "auto" разворачивается в системную тему, явные режимы остаются как есть.
function resolveTheme(mode) {
    if (mode === "light" || mode === "dark") {
        return mode;
    }

    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function applyTheme(mode) {
    const resolved = resolveTheme(mode);
    document.documentElement.setAttribute("data-theme", resolved);

    // Выбранный режим запоминаем, чтобы инлайн-скрипт в index.html применил его до отрисовки
    // и тёмная тема не начиналась со вспышки светлого фона.
    try {
        window.localStorage.setItem("adashboard-theme", mode || "auto");
    } catch (error) {
        // Приватный режим или отключённое хранилище: тема просто не запоминается.
    }
}

function handleSystemThemeChange() {
    // Смена системной темы важна только в режиме "auto".
    if (activeThemeMode === "auto") {
        applyTheme("auto");
    }
}

export function initializeTheme(mode) {
    activeThemeMode = mode || "auto";
    applyTheme(activeThemeMode);

    // Подписку на системную тему заводим один раз: она живёт, пока открыто приложение.
    if (systemThemeMedia === null) {
        systemThemeMedia = window.matchMedia("(prefers-color-scheme: dark)");
        systemThemeMedia.addEventListener("change", handleSystemThemeChange);
    }
}

export function setThemeMode(mode) {
    activeThemeMode = mode || "auto";
    applyTheme(activeThemeMode);
}

// Подставляет адреса фоновых изображений: они приходят из DomainConstants, а CSS-переменные
// собирать адрес из C# не умеют. До вызова функции фон не задан — виден только цвет основы.
export function setBackgroundImages(lightUrl, darkUrl) {
    const style = document.documentElement.style;

    if (lightUrl) {
        style.setProperty("--dashboard-background-light", `url("${lightUrl}")`);
    }

    if (darkUrl) {
        style.setProperty("--dashboard-background-dark", `url("${darkUrl}")`);
    }
}

// ---------------------------------------------------------------------------
// Раскладка категорий (masonry) и перетаскивание.
//
// Категории позиционируются абсолютно: модуль измеряет их высоты и укладывает по колонкам
// сетки, подбирая каждой самое верхнее подходящее место. Число колонок зависит от ширины
// экрана (12 / 6 / 4), ширину категории задают её CSS-переменные --span-* (см. Category).
//
// Порядок укладки ведёт сам модуль (orderIds), а не порядок узлов в DOM. Это важно: после
// перетаскивания новый порядок применяется сразу, а узлы в DOM Blazor переставляет уже потом —
// если бы укладка читала DOM, между отпусканием и перерисовкой порядок успевал откатиться назад
// и категория на кадр мелькала на прежнем месте.
//
// До вызова initializeLayout категории укладывает CSS-grid, поэтому при недоступном модуле
// страница остаётся рабочей. Перетаскивание и изменение ширины работают только в режиме
// редактирования (флаг приходит из PageView через setEditMode).
// ---------------------------------------------------------------------------

const GRID_ELEMENT_ID = "dashboard-grid";

// Перетаскивание начинается после заметного смещения указателя, а на тач-устройстве — ещё
// и после длительного нажатия: иначе обычный жест остался бы обычной прокруткой страницы.
const DRAG_THRESHOLD_PX = 6;
const LONG_PRESS_DELAY_MS = 400;

let gridResizeObserver = null;
let itemResizeObserver = null;
let gridMutationObserver = null;
let layoutFrame = 0;
const observedCategories = new Set();

// Порядок категорий для укладки. Модуль ведёт его сам: после перетаскивания сюда попадает
// порядок, который выбрал пользователь, и укладка сразу считается по нему.
let orderIds = [];

let dotNetRef = null;
let dragState = null;
let editModeEnabled = false;
let syncQueue = [];
let syncInProgress = false;

// Перенос карточки, ожидающий применения в Blazor: карточка скрыта, а на новом месте стоит
// placeholder. Превью держится, пока Blazor не расставит карточки по новому порядку.
let pendingCardDrop = null;

function getGrid() {
    return document.getElementById(GRID_ELEMENT_ID);
}

function parseId(value) {
    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) ? parsed : null;
}

function getColumnCount() {
    const width = window.innerWidth;

    if (width <= 900) {
        return 4;
    }

    if (width <= 1200) {
        return 6;
    }

    return 12;
}

function getSpanVariable() {
    const width = window.innerWidth;

    if (width <= 900) {
        return "--span-mobile";
    }

    if (width <= 1200) {
        return "--span-medium";
    }

    return "--span-desktop";
}

function readSpan(element) {
    const value = Number.parseFloat(getComputedStyle(element).getPropertyValue(getSpanVariable()));
    return Number.isFinite(value) ? value : 1;
}

function getColumnGap(grid) {
    const gap = Number.parseFloat(getComputedStyle(grid).columnGap);
    return Number.isFinite(gap) ? gap : 16;
}

function getCategoryElements() {
    const grid = getGrid();
    if (!grid) {
        return [];
    }

    return Array.from(grid.children).filter((child) => child.classList.contains("dashboard-category"));
}

// Идентификаторы категорий в порядке узлов DOM — это порядок модели, каким его отрисовал Blazor.
function collectCategoryIds() {
    return getCategoryElements()
        .map((element) => parseId(element.dataset.categoryId))
        .filter((id) => id !== null);
}

// Приводит порядок укладки в соответствие с набором категорий: исчезнувшие убирает,
// новые (только что добавленные) дописывает в конец — они и в модели идут последними.
function reconcileOrder() {
    const ids = collectCategoryIds();
    const existing = new Set(ids);

    orderIds = orderIds.filter((id) => existing.has(id));

    const known = new Set(orderIds);
    for (const id of ids) {
        if (!known.has(id)) {
            known.add(id);
            orderIds.push(id);
        }
    }
}

// Категории в порядке укладки: известные — по порядку модуля, остальные — в конце.
function getOrderedCategories() {
    const byId = new Map();

    for (const element of getCategoryElements()) {
        const id = parseId(element.dataset.categoryId);
        if (id !== null && !byId.has(id)) {
            byId.set(id, element);
        }
    }

    const ordered = [];

    for (const id of orderIds) {
        const element = byId.get(id);
        if (element) {
            ordered.push(element);
            byId.delete(id);
        }
    }

    for (const element of byId.values()) {
        ordered.push(element);
    }

    return ordered;
}

// Определяет, перед каким элементом встанет перетаскиваемая категория. Скан в порядке чтения:
// сначала сравниваем по строкам, затем по горизонтали внутри строки.
function findInsertBefore(elements, x, y) {
    for (const element of elements) {
        const rect = element.getBoundingClientRect();

        if (y < rect.top) {
            return element;
        }

        if (y <= rect.bottom && x < rect.left + rect.width / 2) {
            return element;
        }
    }

    return null;
}

// Полная укладка: каждому элементу подбираем самое верхнее место, где помещается его
// ширина, не выходя за уже занятые области колонок.
function pack(entries, columns, columnWidth, gap) {
    const heights = new Array(columns).fill(0);
    const positions = [];

    for (const entry of entries) {
        const span = Math.min(Math.max(entry.span, 1), columns);

        let bestColumn = 0;
        let bestTop = Number.POSITIVE_INFINITY;

        for (let column = 0; column + span <= columns; column++) {
            let top = 0;
            for (let k = column; k < column + span; k++) {
                top = Math.max(top, heights[k]);
            }

            if (top < bestTop - 0.5) {
                bestTop = top;
                bestColumn = column;
            }
        }

        const width = span * columnWidth + (span - 1) * gap;
        positions.push({ element: entry.element, x: bestColumn * (columnWidth + gap), y: bestTop, width });

        const bottom = bestTop + entry.height + gap;
        for (let k = bestColumn; k < bestColumn + span; k++) {
            heights[k] = bottom;
        }
    }

    const maxBottom = entries.length === 0 ? 0 : Math.max(...heights) - gap;
    return { positions, maxBottom: Math.max(0, maxBottom) };
}

// Абсолютное позиционирование ставим здесь, а не в CSS: пока модуль не подключён, категории
// укладывает CSS-grid, и страница остаётся рабочей.
function setPosition(element, x, y, width) {
    const style = element.style;
    const left = `${x}px`;
    const top = `${y}px`;
    const pixelWidth = `${width}px`;

    if (style.position !== "absolute") {
        style.position = "absolute";
    }

    if (style.left !== left) {
        style.left = left;
    }

    if (style.top !== top) {
        style.top = top;
    }

    if (style.width !== pixelWidth) {
        style.width = pixelWidth;
    }
}

function layout() {
    const grid = getGrid();
    if (!grid) {
        return;
    }

    // Во время перетаскивания позиции задаёт превью, а не общая укладка.
    // При изменении ширины категории порядок, наоборот, нужно пересчитывать.
    if (dragState && dragState.active && dragState.kind !== "resize") {
        return;
    }

    const width = grid.clientWidth;
    if (width <= 0) {
        return;
    }

    const columns = getColumnCount();
    const gap = getColumnGap(grid);
    const columnWidth = (width - (columns - 1) * gap) / columns;

    const entries = getOrderedCategories().map((element) => {
        const span = Math.min(Math.max(readSpan(element), 1), columns);
        const itemWidth = span * columnWidth + (span - 1) * gap;

        if (element.style.width !== `${itemWidth}px`) {
            element.style.width = `${itemWidth}px`;
        }

        return { element, span, height: 0 };
    });

    // Форсируем reflow, чтобы высоты измерились уже по заданной ширине.
    void grid.offsetHeight;

    for (const entry of entries) {
        entry.height = entry.element.offsetHeight;
    }

    const { positions, maxBottom } = pack(entries, columns, columnWidth, gap);

    for (const position of positions) {
        setPosition(position.element, position.x, position.y, position.width);
    }

    const height = `${maxBottom}px`;
    if (grid.style.height !== height) {
        grid.style.height = height;
    }
}

// Укладку откладываем до кадра отрисовки: так несколько изменений подряд (например, серия
// мутаций DOM) вызывают один пересчёт, но он всё равно успевает до отрисовки кадра.
function scheduleLayout() {
    if (layoutFrame !== 0) {
        return;
    }

    layoutFrame = window.requestAnimationFrame(() => {
        layoutFrame = 0;
        layout();
    });
}

function syncObservedCategories() {
    const grid = getGrid();
    if (!grid || !itemResizeObserver) {
        return;
    }

    for (const element of grid.children) {
        if (element.classList.contains("dashboard-category") && !observedCategories.has(element)) {
            observedCategories.add(element);
            itemResizeObserver.observe(element);
        }
    }

    for (const element of observedCategories) {
        if (!element.isConnected) {
            itemResizeObserver.unobserve(element);
            observedCategories.delete(element);
        }
    }
}

// Снимает позиционирование, поставленное модулем: категории снова укладывает CSS-grid.
function clearManagedLayout() {
    const grid = getGrid();
    if (!grid) {
        return;
    }

    grid.classList.remove("dashboard-grid--managed");
    grid.style.height = "";

    for (const element of grid.children) {
        if (!element.classList.contains("dashboard-category")) {
            continue;
        }

        const style = element.style;
        style.position = "";
        style.left = "";
        style.top = "";
        style.width = "";
    }
}

export function initializeLayout() {
    disposeLayout();

    const grid = getGrid();
    if (!grid) {
        return;
    }

    // Признак того, что раскладку ведёт модуль: пока класса нет, категории укладывает CSS-grid.
    // В управляемом режиме у категорий не должно быть grid-размещения — иначе Firefox берёт
    // grid-область контейнером позиционирования и категории уезжают вправо (см. Category).
    grid.classList.add("dashboard-grid--managed");

    // Отправная точка порядка укладки — порядок, в котором категории отрисовал Blazor.
    orderIds = collectCategoryIds();

    // Ширина сетки меняется при ресайзе и повороте экрана — раскладываем заново.
    gridResizeObserver = new ResizeObserver(() => scheduleLayout());
    gridResizeObserver.observe(grid);

    // Высота категории меняется от содержимого (например, добавили ссылку) — тоже перекладываем.
    itemResizeObserver = new ResizeObserver(() => scheduleLayout());

    // Появление, удаление и перестановка категорий. Перестановку делает Blazor уже по порядку
    // модуля, поэтому после перерисовки достаточно пересчитать укладку.
    gridMutationObserver = new MutationObserver(() => {
        reconcileOrder();
        syncObservedCategories();
        scheduleLayout();
    });
    gridMutationObserver.observe(grid, { childList: true });

    syncObservedCategories();

    // Первую укладку делаем сразу, в том же кадре: иначе между сменой режима сетки
    // и расстановкой категорий мелькнула бы статичная раскладка.
    layout();
}

export function disposeLayout() {
    if (gridResizeObserver) {
        gridResizeObserver.disconnect();
        gridResizeObserver = null;
    }

    if (itemResizeObserver) {
        itemResizeObserver.disconnect();
        itemResizeObserver = null;
    }

    if (gridMutationObserver) {
        gridMutationObserver.disconnect();
        gridMutationObserver = null;
    }

    if (layoutFrame !== 0) {
        window.cancelAnimationFrame(layoutFrame);
        layoutFrame = 0;
    }

    observedCategories.clear();
    orderIds = [];
    clearManagedLayout();
}

// ---------------------------------------------------------------------------
// Перетаскивание категорий и карточек, изменение ширины категории.
// ---------------------------------------------------------------------------

function sameOrder(a, b) {
    if (a.length !== b.length) {
        return false;
    }

    for (let i = 0; i < a.length; i++) {
        if (a[i] !== b[i]) {
            return false;
        }
    }

    return true;
}

function createPlaceholder(rect) {
    const placeholder = document.createElement("div");
    placeholder.className = "dnd-placeholder dnd-placeholder-category";
    placeholder.style.position = "absolute";
    placeholder.style.width = `${rect.width}px`;
    placeholder.style.height = `${rect.height}px`;
    placeholder.setAttribute("aria-hidden", "true");
    return placeholder;
}

// Placeholder карточки: стоит в сетке карточек категории и занимает столько же колонок, сколько
// сама карточка. Ширину карточка задаёт span-переменными, поэтому переносим их на placeholder —
// иначе широкая карточка заняла бы одну колонку.
function createCardPlaceholder(item, rect) {
    const placeholder = document.createElement("div");
    placeholder.className = "dnd-placeholder dnd-placeholder-card";

    for (const name of ["--card-span-desktop", "--card-span-medium", "--card-span-mobile"]) {
        const value = item.style.getPropertyValue(name);
        if (value) {
            placeholder.style.setProperty(name, value);
        }
    }

    placeholder.style.height = `${rect.height}px`;
    placeholder.setAttribute("aria-hidden", "true");
    return placeholder;
}

function createClone(item, rect, dataAttribute) {
    const clone = item.cloneNode(true);
    clone.classList.add("dnd-clone");

    if (dataAttribute) {
        clone.removeAttribute(dataAttribute);
    }

    clone.style.position = "fixed";
    clone.style.top = "0";
    clone.style.left = "0";
    clone.style.margin = "0";
    clone.style.width = `${rect.width}px`;
    clone.style.height = `${rect.height}px`;
    clone.style.transform = `translate(${rect.left}px, ${rect.top}px)`;
    return clone;
}

function startCategorySession(category, event) {
    dragState = {
        kind: "category",
        item: category,
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        rect: category.getBoundingClientRect(),
        active: false,
        pressTimer: 0,
        previewIds: null
    };

    document.addEventListener("pointermove", onPointerMove, true);
    document.addEventListener("pointerup", onPointerUp, true);
    document.addEventListener("pointercancel", onPointerCancel, true);
}

// Карточку мышью и стилусом переносим после заметного смещения, а пальцем — после длительного
// нажатия: обычный жест по карточке должен остаться прокруткой страницы.
function startCardSession(card, container, event) {
    dragState = {
        kind: "card",
        item: card,
        container,
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        rect: card.getBoundingClientRect(),
        active: false,
        pressTimer: 0,
        previewIds: null
    };

    if (event.pointerType === "touch") {
        dragState.pressTimer = window.setTimeout(() => {
            const state = dragState;

            if (state && state.pointerId === event.pointerId && !state.active) {
                activateDrag(state.startX, state.startY);
            }
        }, LONG_PRESS_DELAY_MS);
    }

    document.addEventListener("pointermove", onPointerMove, true);
    document.addEventListener("pointerup", onPointerUp, true);
    document.addEventListener("pointercancel", onPointerCancel, true);
}

function clearPressTimer(state) {
    if (state && state.pressTimer) {
        window.clearTimeout(state.pressTimer);
        state.pressTimer = 0;
    }
}

function endSession() {
    clearPressTimer(dragState);
    stopEdgeScroll();

    document.removeEventListener("pointermove", onPointerMove, true);
    document.removeEventListener("pointerup", onPointerUp, true);
    document.removeEventListener("pointercancel", onPointerCancel, true);
    dragState = null;
}

// Ширину категории тянем маркером у правого края: она задаётся в колонках сетки
// и округляется до целого числа колонок текущего брейкпоинта.
function startResizeSession(category, event) {
    dragState = {
        kind: "resize",
        item: category,
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        active: false,
        categoryId: parseId(category.dataset.categoryId),
        startSpan: readSpan(category),
        currentSpan: null
    };

    document.addEventListener("pointermove", onPointerMove, true);
    document.addEventListener("pointerup", onPointerUp, true);
    document.addEventListener("pointercancel", onPointerCancel, true);
}

function handleResizeMove(event) {
    const state = dragState;

    // Ресайз начинается только после заметного смещения — случайный клик ничего не меняет.
    if (!state.active) {
        if (Math.abs(event.clientX - state.startX) < DRAG_THRESHOLD_PX) {
            return;
        }

        state.active = true;
        state.currentSpan = state.startSpan;
        document.body.classList.add("category-resizing");
    }

    event.preventDefault();
    updateResize(event);
}

function updateResize(event) {
    const grid = getGrid();
    if (!grid) {
        return;
    }

    const columns = getColumnCount();
    const gap = getColumnGap(grid);
    const columnWidth = (grid.clientWidth - (columns - 1) * gap) / columns;
    const pitch = columnWidth + gap;

    const delta = event.clientX - dragState.startX;
    const span = Math.min(Math.max(Math.round(dragState.startSpan + delta / pitch), 1), columns);

    dragState.currentSpan = span;
    setCategorySpan(dragState.item, span);
    scheduleLayout();
}

// Меняем переменные только текущего брейкпоинта: после сохранения Blazor перерисует
// категорию и выставит значения для всех брейкпоинтов из модели.
function setCategorySpan(element, span) {
    const spanVariable = getSpanVariable();
    element.style.setProperty(spanVariable, span);
    element.style.setProperty(spanVariable.replace("--span-", "--cards-"), span);
}

// Обратное преобразование количества колонок текущего брейкпоинта в сохраняемую ширину
// категории (она задаётся в колонках desktop-сетки из 12 колонок).
function categorySpanToWidth(span) {
    const width = getColumnCount() === 6 ? span * 2 : span;
    return Math.min(Math.max(width, 1), 12);
}

function finishResize() {
    const state = dragState;

    if (!state.active) {
        endSession();
        return;
    }

    const span = state.currentSpan ?? state.startSpan;
    const width = categorySpanToWidth(span);
    const changed = span !== state.startSpan && state.categoryId !== null;

    document.body.classList.remove("category-resizing");
    endSession();
    scheduleLayout();

    if (changed && dotNetRef) {
        enqueueSync(() => dotNetRef.invokeMethodAsync("OnCategoryResized", state.categoryId, width));
    }
}

function cancelResize() {
    const state = dragState;

    if (state) {
        // Изменение не сохранено — возвращаем исходную ширину.
        setCategorySpan(state.item, state.startSpan);
        document.body.classList.remove("category-resizing");
    }

    endSession();
    scheduleLayout();
}

function onPointerDown(event) {
    // Перетаскивание доступно только в режиме редактирования, по одной сессии за раз.
    if (event.button !== 0 || syncInProgress || dragState || !editModeEnabled) {
        return;
    }

    if (!(event.target instanceof Element)) {
        return;
    }

    const resizeHandle = event.target.closest(".category-resize-handle");
    if (resizeHandle) {
        const category = resizeHandle.closest(".dashboard-category");
        if (category) {
            startResizeSession(category, event);
            return;
        }
    }

    const handle = event.target.closest(".drag-handle");
    if (handle) {
        const category = handle.closest(".dashboard-category");
        if (category) {
            startCategorySession(category, event);
            return;
        }
    }

    const card = event.target.closest(".dashboard-card");
    if (card) {
        const container = card.closest(".category-cards");
        if (container) {
            startCardSession(card, container, event);
        }
    }
}

// Убирает превью перетаскивания: placeholder и фантом, возвращает элемент в раскладку.
// Позиции после этого расставляет layout() — он же возвращает прежнее место при отмене.
function cleanup() {
    const state = dragState;
    if (!state) {
        return;
    }

    if (state.placeholder && state.placeholder.parentElement) {
        state.placeholder.remove();
    }

    if (state.clone && state.clone.parentElement) {
        state.clone.remove();
    }

    if (state.item) {
        state.item.style.display = "";
    }

    document.body.classList.remove("dnd-active");
    state.placeholder = null;
    state.clone = null;
}

function activateDrag(x, y) {
    const state = dragState;

    state.active = true;
    state.offsetX = x - state.rect.left;
    state.offsetY = y - state.rect.top;

    clearPressTimer(state);

    if (state.kind === "card") {
        // Карточка живёт в сетке карточек своей категории: placeholder встаёт рядом с ней.
        state.placeholder = createCardPlaceholder(state.item, state.rect);
        state.container.insertBefore(state.placeholder, state.item);
    } else {
        const grid = getGrid();
        const placeholder = createPlaceholder(state.rect);

        state.sourceSpan = readSpan(state.item);
        state.sourceHeight = state.item.offsetHeight;

        if (grid) {
            grid.appendChild(placeholder);
        }

        state.placeholder = placeholder;
    }

    // Фантом создаём до скрытия источника, иначе он унаследует display:none.
    const clone = createClone(state.item, state.rect, state.kind === "card" ? "data-card-id" : null);
    document.body.appendChild(clone);
    state.clone = clone;

    // Исходный элемент убираем из потока: его место занимает placeholder.
    state.item.style.display = "none";

    document.body.classList.add("dnd-active");
    updateDrag(x, y);
    startEdgeScroll();
}

// Пересобирает раскладку под указатель: placeholder встаёт на место, куда попадёт категория,
// а остальные разъезжаются по колонкам. Порядок при этом берётся из порядка укладки модуля.
function updateCategoryTarget(x, y) {
    const state = dragState;
    const grid = getGrid();
    if (!grid) {
        return;
    }

    const others = getOrderedCategories().filter((element) => element !== state.item);
    const before = findInsertBefore(others, x, y);
    const index = before ? others.indexOf(before) : others.length;

    const columns = getColumnCount();
    const gap = getColumnGap(grid);
    const columnWidth = (grid.clientWidth - (columns - 1) * gap) / columns;

    const placeholderEntry = {
        element: state.placeholder,
        span: state.sourceSpan,
        height: state.sourceHeight,
        isPlaceholder: true
    };

    const entries = [];
    for (let i = 0; i < others.length; i++) {
        if (i === index) {
            entries.push(placeholderEntry);
        }

        const element = others[i];
        entries.push({ element, span: readSpan(element), height: element.offsetHeight });
    }

    if (index >= others.length) {
        entries.push(placeholderEntry);
    }

    const { positions, maxBottom } = pack(entries, columns, columnWidth, gap);

    for (const position of positions) {
        setPosition(position.element, position.x, position.y, position.width);
    }

    grid.style.height = `${maxBottom}px`;

    const sourceId = parseId(state.item.dataset.categoryId);
    state.previewIds = entries
        .map((entry) => (entry.isPlaceholder ? sourceId : parseId(entry.element.dataset.categoryId)))
        .filter((id) => id !== null);
}

// ---------------------------------------------------------------------------
// Перенос карточки: placeholder встаёт в сетку карточек категории под указателем.
// ---------------------------------------------------------------------------

// Сетка карточек под указателем. Сначала проверяем точное попадание в сетку по геометрии,
// а если указатель в стороне (например, над заголовком категории) — берём сетку категории
// под указателем: сбрасывать карточку разрешено в любое место категории.
function findCardContainer(x, y) {
    for (const container of document.querySelectorAll(".category-cards")) {
        const rect = container.getBoundingClientRect();
        if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom) {
            return container;
        }
    }

    const under = document.elementFromPoint(x, y);
    const category = under?.closest(".dashboard-category");
    return category ? category.querySelector(".category-cards") : null;
}

// Ставит placeholder перед нужной карточкой. Когда положение не меняется, DOM не трогаем:
// иначе он менялся бы на каждом движении указателя.
function movePlaceholder(container, placeholder, before) {
    if (placeholder.parentElement !== container || placeholder.nextElementSibling !== before) {
        container.insertBefore(placeholder, before);
    }
}

function updateCardTarget(x, y) {
    const state = dragState;
    const container = findCardContainer(x, y);
    if (!container) {
        return;
    }

    const cards = Array.from(container.children).filter(
        (child) => child.classList.contains("dashboard-card") && child !== state.item);

    movePlaceholder(container, state.placeholder, findInsertBefore(cards, x, y));
}

// Идентификаторы карточек контейнера в порядке DOM. Перетаскиваемую карточку пропускаем,
// а на место placeholder подставляем её идентификатор — так получается порядок после сброса.
function collectCardIds(container, item, placeholder) {
    const ids = [];
    const movedId = item ? parseId(item.dataset.cardId) : null;

    for (const child of container.children) {
        if (item && child === item) {
            continue;
        }

        if (placeholder && child === placeholder) {
            if (movedId !== null) {
                ids.push(movedId);
            }

            continue;
        }

        if (child.classList.contains("dashboard-card")) {
            const id = parseId(child.dataset.cardId);
            if (id !== null) {
                ids.push(id);
            }
        }
    }

    return ids;
}

function updateDrag(x, y) {
    const state = dragState;

    // Последнее положение указателя нужно автопрокрутке: она пересчитывает цель без движений.
    state.lastX = x;
    state.lastY = y;

    if (state.clone) {
        state.clone.style.transform = `translate(${x - state.offsetX}px, ${y - state.offsetY}px)`;
    }

    if (state.kind === "card") {
        updateCardTarget(x, y);
    } else {
        updateCategoryTarget(x, y);
    }
}

function finishDrag() {
    const state = dragState;
    if (!state) {
        return;
    }

    if (state.kind === "card") {
        finishCardDrag(state);
        return;
    }

    const currentIds = getOrderedCategories()
        .map((element) => parseId(element.dataset.categoryId))
        .filter((id) => id !== null);

    const orderedIds = state.previewIds ?? currentIds;
    const changed = !sameOrder(orderedIds, currentIds);

    if (changed) {
        // Новый порядок становится порядком укладки: layout() ниже сразу расставит категории
        // по нему, не дожидаясь перерисовки Blazor (узлы в DOM она переставит следом).
        orderIds = orderedIds;
    }

    cleanup();
    endSession();

    // Укладка в том же кадре: на экране остаётся ровно то, что показало превью.
    layout();

    if (changed && dotNetRef) {
        enqueueSync(() => dotNetRef.invokeMethodAsync("OnCategoriesReordered", orderedIds));
    }
}

// Порядок карточек в категории задаёт порядок узлов в DOM, поэтому, в отличие от категорий,
// новый порядок применяет Blazor — по модели. Пока он не пересобрал раскладку, превью держится:
// карточка остаётся скрытой и встаёт на своё место уже с новым порядком.
function finishCardDrag(state) {
    const sourceContainer = state.container;
    const targetContainer = state.placeholder.parentElement;
    const sourceCategoryId = parseId(sourceContainer.dataset.categoryId);
    const targetCategoryId = targetContainer ? parseId(targetContainer.dataset.categoryId) : null;

    // Фантом убираем сразу: дальше на экране только placeholder и скрытая карточка.
    if (state.clone && state.clone.parentElement) {
        state.clone.remove();
        state.clone = null;
    }

    document.body.classList.remove("dnd-active");

    if (sourceCategoryId === null || targetCategoryId === null || !state.placeholder.isConnected) {
        cleanup();
        endSession();
        return;
    }

    const sameContainer = sourceContainer === targetContainer;
    const targetIds = collectCardIds(targetContainer, state.item, state.placeholder);
    const sourceIds = sameContainer ? targetIds : collectCardIds(sourceContainer, state.item, null);

    // Порядок не изменился: карточку сразу возвращаем на место, сохранять нечего.
    if (sameContainer && sameOrder(targetIds, collectCardIds(targetContainer, null, null))) {
        cleanup();
        endSession();
        return;
    }

    // Карточку и placeholder оставляем на месте: их уберёт finalizeCardDrop после применения порядка.
    pendingCardDrop = { item: state.item, placeholder: state.placeholder };
    state.placeholder = null;
    endSession();

    if (!dotNetRef) {
        finalizeCardDrop();
        return;
    }

    enqueueSync(async () => {
        try {
            await dotNetRef.invokeMethodAsync("OnCardsReordered", sourceCategoryId, targetCategoryId, sourceIds, targetIds);
        } finally {
            finalizeCardDrop();
        }
    });
}

// Возвращает карточку в раскладку после того, как Blazor применил новый порядок. Карточка,
// перенесённая в другую категорию, к этому моменту уже пересоздана Blazor — тогда исходный
// узел просто исчез из документа.
function finalizeCardDrop() {
    const pending = pendingCardDrop;
    if (!pending) {
        return;
    }

    pendingCardDrop = null;

    if (pending.item.isConnected) {
        pending.item.style.display = "";
    }

    if (pending.placeholder.parentElement) {
        pending.placeholder.remove();
    }
}

function onPointerMove(event) {
    if (!dragState || event.pointerId !== dragState.pointerId) {
        return;
    }

    if (dragState.kind === "resize") {
        handleResizeMove(event);
        return;
    }

    if (!dragState.active) {
        const dx = event.clientX - dragState.startX;
        const dy = event.clientY - dragState.startY;

        if (Math.hypot(dx, dy) < DRAG_THRESHOLD_PX) {
            return;
        }

        // На тач-устройстве до длительного нажатия жест остаётся прокруткой страницы.
        if (dragState.pressTimer) {
            endSession();
            return;
        }

        activateDrag(event.clientX, event.clientY);
        return;
    }

    event.preventDefault();
    updateDrag(event.clientX, event.clientY);
}

function onPointerUp(event) {
    if (!dragState || event.pointerId !== dragState.pointerId) {
        return;
    }

    if (dragState.kind === "resize") {
        finishResize();
        return;
    }

    if (dragState.active) {
        finishDrag();
    } else {
        endSession();
    }
}

function onPointerCancel(event) {
    if (!dragState || event.pointerId !== dragState.pointerId) {
        return;
    }

    if (dragState.kind === "resize") {
        cancelResize();
        return;
    }

    cleanup();
    endSession();
    layout();
}

function onDragStart(event) {
    // Нативная ссылка «тянется» браузером и рисует полупрозрачный фантом карточки — даже вне
    // режима редактирования, поэтому у карточек нативный drag подавляем всегда. В режиме
    // редактирования — ещё и у остальных элементов: их фантом мешал бы перетаскиванию.
    if (editModeEnabled || event.target instanceof Element && event.target.closest(".dashboard-card")) {
        event.preventDefault();
    }
}

// Прокрутку страницы во время переноса карточки пальцем подавляем вручную: карточкам
// touch-action не ограничиваем, иначе страница не листалась бы вне перетаскивания. Подавляем
// и во время ожидания длительного нажатия: браузер отменяет прокрутку только по первому
// touchmove жеста, а дрожание пальца на удержании иначе успело бы запустить скролл.
function onTouchMove(event) {
    if (dragState && dragState.kind === "card" && (dragState.active || dragState.pressTimer)) {
        event.preventDefault();
    }
}

// Долгое нажатие по ссылке — начало переноса карточки, а не системное меню ссылки.
function onContextMenu(event) {
    if (editModeEnabled && dragState && dragState.kind === "card") {
        event.preventDefault();
    }
}

// Край экрана, у которого при переносе начинается автопрокрутка, и её максимальная скорость
// за кадр: прокрутка во время переноса выключена, и добраться до невидимой части страницы
// можно только подтягиванием к краю.
const EDGE_SCROLL_ZONE_PX = 90;
const EDGE_SCROLL_MAX_STEP_PX = 24;

let edgeScrollFrame = 0;

function edgeScrollStep() {
    edgeScrollFrame = 0;

    const state = dragState;
    if (!state || !state.active || state.kind === "resize") {
        return;
    }

    const y = state.lastY;
    const height = window.innerHeight;
    let delta = 0;

    if (y < EDGE_SCROLL_ZONE_PX) {
        delta = -Math.ceil(((EDGE_SCROLL_ZONE_PX - y) / EDGE_SCROLL_ZONE_PX) * EDGE_SCROLL_MAX_STEP_PX);
    } else if (y > height - EDGE_SCROLL_ZONE_PX) {
        delta = Math.ceil(((y - (height - EDGE_SCROLL_ZONE_PX)) / EDGE_SCROLL_ZONE_PX) * EDGE_SCROLL_MAX_STEP_PX);
    }

    if (delta !== 0) {
        const before = window.scrollY;
        window.scrollBy(0, delta);

        // Страница сдвинулась — под указателем оказались другие элементы, цель переносим заново.
        if (window.scrollY !== before) {
            updateDrag(state.lastX, state.lastY);
        }
    }

    edgeScrollFrame = window.requestAnimationFrame(edgeScrollStep);
}

function startEdgeScroll() {
    if (edgeScrollFrame === 0) {
        edgeScrollFrame = window.requestAnimationFrame(edgeScrollStep);
    }
}

function stopEdgeScroll() {
    if (edgeScrollFrame !== 0) {
        window.cancelAnimationFrame(edgeScrollFrame);
        edgeScrollFrame = 0;
    }
}

function onKeyDown(event) {
    if (event.key !== "Escape" || !dragState) {
        return;
    }

    if (dragState.kind === "resize") {
        cancelResize();
        return;
    }

    cleanup();
    endSession();
    layout();
}

function enqueueSync(task) {
    syncQueue.push(task);

    if (!syncInProgress) {
        void processSyncQueue();
    }
}

// Изменения уходят в Blazor по одному: одновременно больше одной записи не бывает.
async function processSyncQueue() {
    syncInProgress = true;

    try {
        while (syncQueue.length > 0) {
            const task = syncQueue.shift();
            if (task) {
                await task();
            }
        }
    } catch (error) {
        console.error("Ошибка синхронизации порядка и ширины категорий dashboard", error);
    } finally {
        syncInProgress = false;

        if (syncQueue.length > 0) {
            void processSyncQueue();
        }
    }
}

export function initializeDragAndDrop(ref) {
    disposeDragAndDrop();

    dotNetRef = ref;

    document.addEventListener("pointerdown", onPointerDown, true);
    document.addEventListener("dragstart", onDragStart, true);
    document.addEventListener("keydown", onKeyDown, true);
    document.addEventListener("touchmove", onTouchMove, { capture: true, passive: false });
    document.addEventListener("contextmenu", onContextMenu, true);
}

// Режим редактирования приходит из Blazor: вне него перетаскивание и ресайз не начинаются.
export function setEditMode(enabled) {
    editModeEnabled = !!(enabled);

    // При выключении режима активное перетаскивание немедленно отменяется.
    if (!editModeEnabled && dragState) {
        cleanup();
        endSession();
        layout();
    }
}

export function disposeDragAndDrop() {
    document.removeEventListener("pointerdown", onPointerDown, true);
    document.removeEventListener("dragstart", onDragStart, true);
    document.removeEventListener("keydown", onKeyDown, true);
    document.removeEventListener("touchmove", onTouchMove, { capture: true });
    document.removeEventListener("contextmenu", onContextMenu, true);

    cleanup();
    endSession();
    finalizeCardDrop();
    document.body.classList.remove("category-resizing");

    dotNetRef = null;
    syncQueue = [];
    syncInProgress = false;
    editModeEnabled = false;
}
