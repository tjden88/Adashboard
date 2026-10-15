// Оформление dashboard: режим темы, фоновые изображения страницы, раскладка категорий
// и их перетаскивание.
//
// Модуль оформления подключает MainLayout, раскладку и перетаскивание — PageView.
// Перетаскивание карточек между категориями добавит часть card-view.

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
// Раскладка и перетаскивание категорий.
//
// Категории позиционируются абсолютно: модуль измеряет их высоты и укладывает по колонкам
// сетки, подбирая каждой самое верхнее подходящее место. Число колонок зависит от ширины
// экрана (12 / 6 / 4), ширину категории задают её CSS-переменные --span-* (см. Category).
// До вызова initializeLayout категории укладывает CSS-grid, поэтому при недоступном модуле
// страница остаётся рабочей.
//
// Перетаскивание и изменение ширины работают только в режиме редактирования (флаг приходит
// из PageView через setEditMode), а перетаскивание идёт с Pointer Events — мыши и касания.
// ---------------------------------------------------------------------------

const GRID_ELEMENT_ID = "dashboard-grid";
const DRAG_THRESHOLD_PX = 6;

let gridResizeObserver = null;
let itemResizeObserver = null;
let gridMutationObserver = null;
let layoutFrame = 0;
const observedCategories = new Set();

let dotNetRef = null;
let dragState = null;
let editModeEnabled = false;
let syncQueue = [];
let syncInProgress = false;

// После drop категории превью держится до перерисовки Blazor, чтобы не было видимого
// отката к старому порядку.
let pendingCategoryDrop = null;
let pendingCategoryDropTimer = 0;

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

// Элементы раскладки: скрытая при перетаскивании категория в укладке не участвует.
function getCategories() {
    const grid = getGrid();
    if (!grid) {
        return [];
    }

    return Array.from(grid.children).filter(
        (child) => child.classList.contains("dashboard-category") && child.style.display !== "none");
}

// Определяет, перед каким элементом вставить placeholder. Скан в порядке чтения:
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

    // Во время перетаскивания позиции задаёт превью/placeholder, а не общий layout.
    // При изменении ширины категории порядок, наоборот, нужно пересчитывать.
    if (dragState && dragState.active && dragState.kind !== "resize") {
        return;
    }

    // Пока держится превью после drop, старый порядок не перестраиваем.
    if (pendingCategoryDrop) {
        return;
    }

    const width = grid.clientWidth;
    if (width <= 0) {
        return;
    }

    const columns = getColumnCount();
    const gap = getColumnGap(grid);
    const columnWidth = (width - (columns - 1) * gap) / columns;
    const categories = getCategories();

    const entries = categories.map((element) => {
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

    // Ширина сетки меняется при ресайзе и повороте экрана — раскладываем заново.
    gridResizeObserver = new ResizeObserver(() => scheduleLayout());
    gridResizeObserver.observe(grid);

    // Высота категории меняется от содержимого (например, добавили ссылку) — тоже перекладываем.
    itemResizeObserver = new ResizeObserver(() => scheduleLayout());

    // Появление и удаление категорий: раскладываем и заводим наблюдение за новыми.
    gridMutationObserver = new MutationObserver(() => {
        if (pendingCategoryDrop) {
            finalizeCategoryDrop();
            return;
        }

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
    clearManagedLayout();
}

// ---------------------------------------------------------------------------
// Перетаскивание и изменение ширины категорий.
// ---------------------------------------------------------------------------

function createPlaceholder(rect) {
    const placeholder = document.createElement("div");
    placeholder.className = "dnd-placeholder dnd-placeholder-category";
    placeholder.style.position = "absolute";
    placeholder.style.width = `${rect.width}px`;
    placeholder.style.height = `${rect.height}px`;
    placeholder.setAttribute("aria-hidden", "true");
    return placeholder;
}

function createClone(item, rect) {
    const clone = item.cloneNode(true);
    clone.classList.add("dnd-clone");
    clone.removeAttribute("data-category-id");
    clone.style.position = "fixed";
    clone.style.top = "0";
    clone.style.left = "0";
    clone.style.margin = "0";
    clone.style.width = `${rect.width}px`;
    clone.style.height = `${rect.height}px`;
    clone.style.transform = `translate(${rect.left}px, ${rect.top}px)`;
    return clone;
}

function startSession(item, container, event) {
    dragState = {
        kind: "category",
        item,
        container,
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        rect: item.getBoundingClientRect(),
        active: false,
        previewIds: null
    };

    document.addEventListener("pointermove", onPointerMove, true);
    document.addEventListener("pointerup", onPointerUp, true);
    document.addEventListener("pointercancel", onPointerCancel, true);
}

function endSession() {
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
            startSession(category, category.parentElement, event);
        }
    }
}

function activateDrag(event) {
    const state = dragState;
    const rect = state.rect;
    const grid = getGrid();

    state.active = true;
    state.offsetX = event.clientX - rect.left;
    state.offsetY = event.clientY - rect.top;
    state.sourceSpan = readSpan(state.item);
    state.sourceHeight = state.item.offsetHeight;

    const placeholder = createPlaceholder(rect);
    if (grid) {
        grid.appendChild(placeholder);
    }

    state.placeholder = placeholder;

    // Фантом создаём до скрытия источника, иначе он унаследует display:none.
    const clone = createClone(state.item, rect);
    document.body.appendChild(clone);
    state.clone = clone;

    // Исходный элемент убираем из потока: его место занимает placeholder.
    state.item.style.display = "none";

    document.body.classList.add("dnd-active");
    updateDrag(event);
}

function updateCategoryTarget(x, y) {
    const state = dragState;
    const grid = getGrid();
    if (!grid) {
        return;
    }

    const categories = getCategories();
    const before = findInsertBefore(categories, x, y);
    const index = before ? categories.indexOf(before) : categories.length;

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
    for (let i = 0; i < categories.length; i++) {
        if (i === index) {
            entries.push(placeholderEntry);
        }

        const element = categories[i];
        entries.push({ element, span: readSpan(element), height: element.offsetHeight });
    }

    if (index >= categories.length) {
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

function updateDrag(event) {
    const state = dragState;
    const x = event.clientX;
    const y = event.clientY;

    if (state.clone) {
        state.clone.style.transform = `translate(${x - state.offsetX}px, ${y - state.offsetY}px)`;
    }

    updateCategoryTarget(x, y);
}

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

function collectCategoryOrder(grid) {
    const ids = [];

    for (const child of grid.children) {
        if (child.classList.contains("dashboard-category")) {
            const id = parseId(child.dataset.categoryId);
            if (id !== null) {
                ids.push(id);
            }
        }
    }

    return ids;
}

function finalizeCategoryDrop() {
    if (!pendingCategoryDrop) {
        return;
    }

    const { item, placeholder } = pendingCategoryDrop;
    pendingCategoryDrop = null;

    if (pendingCategoryDropTimer !== 0) {
        window.clearTimeout(pendingCategoryDropTimer);
        pendingCategoryDropTimer = 0;
    }

    if (item && placeholder) {
        if (placeholder.style.left !== "") {
            item.style.left = placeholder.style.left;
            item.style.top = placeholder.style.top;
            item.style.width = placeholder.style.width;
        }

        item.style.display = "";
    }

    if (placeholder && placeholder.parentElement) {
        placeholder.remove();
    }

    scheduleLayout();
}

function finishDrag() {
    const state = dragState;
    if (!state) {
        return;
    }

    const { item, placeholder } = state;
    const grid = getGrid();

    const orderedIds = state.previewIds ?? (grid ? collectCategoryOrder(grid) : []);

    // Фантом убираем сразу, а placeholder и скрытый источник — после того,
    // как Blazor применит новый порядок (чтобы не было отката позиций).
    if (state.clone && state.clone.parentElement) {
        state.clone.remove();
    }

    document.body.classList.remove("dnd-active");
    state.placeholder = null;
    state.clone = null;

    pendingCategoryDrop = { item, placeholder };
    pendingCategoryDropTimer = window.setTimeout(finalizeCategoryDrop, 800);

    endSession();

    if (dotNetRef && orderedIds.length > 0) {
        enqueueSync(() => dotNetRef.invokeMethodAsync("OnCategoriesReordered", orderedIds));
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

        activateDrag(event);
        return;
    }

    event.preventDefault();
    updateDrag(event);
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
}

function onDragStart(event) {
    // В режиме редактирования нативный drag-and-drop у карточки-ссылки рисует фантом ссылки:
    // это мешает перетаскиванию категорий, поэтому подавляем.
    if (editModeEnabled) {
        event.preventDefault();
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
    scheduleLayout();
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
}

// Режим редактирования приходит из Blazor: вне него перетаскивание и ресайз не начинаются.
export function setEditMode(enabled) {
    editModeEnabled = !!(enabled);

    // При выключении режима активное перетаскивание немедленно отменяется.
    if (!editModeEnabled && dragState) {
        if (dragState.active) {
            cleanup();
        }

        endSession();
        scheduleLayout();
    }
}

export function disposeDragAndDrop() {
    document.removeEventListener("pointerdown", onPointerDown, true);
    document.removeEventListener("dragstart", onDragStart, true);
    document.removeEventListener("keydown", onKeyDown, true);

    cleanup();
    endSession();
    finalizeCategoryDrop();
    document.body.classList.remove("category-resizing");

    dotNetRef = null;
    syncQueue = [];
    syncInProgress = false;
    editModeEnabled = false;
}
