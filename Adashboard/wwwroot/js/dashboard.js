let activeThemeMode = "auto";
let systemThemeMedia = null;

function resolveTheme(mode) {
    if (mode === "light" || mode === "dark") {
        return mode;
    }

    const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    return prefersDark ? "dark" : "light";
}

function applyTheme(mode) {
    const resolved = resolveTheme(mode);
    document.documentElement.setAttribute("data-theme", resolved);
}

function handleSystemThemeChange() {
    if (activeThemeMode === "auto") {
        applyTheme("auto");
    }
}

export function initializeTheme(mode) {
    activeThemeMode = mode || "auto";
    applyTheme(activeThemeMode);

    if (systemThemeMedia === null) {
        systemThemeMedia = window.matchMedia("(prefers-color-scheme: dark)");
        systemThemeMedia.addEventListener("change", handleSystemThemeChange);
    }
}

export function setThemeMode(mode) {
    activeThemeMode = mode;
    applyTheme(mode);
}

// Раскладка категорий и собственный drag-and-drop на Pointer Events.
// Категории позиционируются абсолютно (masonry-алгоритм), порядок списков во время
// перетаскивания меняет только Blazor — это исключает рассинхрон DOM.
const DRAG_THRESHOLD_PX = 6;

let dotNetRef = null;
let dragState = null;
let suppressClick = false;
let syncQueue = [];
let syncInProgress = false;
// Перетаскивание включено только в режиме редактирования.
let editModeEnabled = false;

let gridResizeObserver = null;
let itemResizeObserver = null;
let gridMutationObserver = null;
let layoutFrame = 0;
const observedCategories = new Set();

// После drop категории превью держится до перерисовки Blazor, чтобы не было
// видимого отката к старому порядку.
let pendingCategoryDrop = null;
let pendingCategoryDropTimer = 0;

function getGrid() {
    return document.getElementById("dashboard-grid");
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

function setPosition(element, x, y, width) {
    const left = `${x}px`;
    const top = `${y}px`;
    const pixelWidth = `${width}px`;

    if (element.style.left !== left) {
        element.style.left = left;
    }

    if (element.style.top !== top) {
        element.style.top = top;
    }

    if (element.style.width !== pixelWidth) {
        element.style.width = pixelWidth;
    }
}

function layout() {
    const grid = getGrid();
    if (!grid) {
        return;
    }

    // Во время перетаскивания позиции задаёт превью/placeholder, а не общий layout.
    if (dragState && dragState.active) {
        return;
    }

    // Пока держится превью после drop, старый порядок не раскладываем.
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

function createPlaceholder(kind, source, rect) {
    const placeholder = document.createElement("div");

    if (kind === "category") {
        placeholder.className = "dnd-placeholder dnd-placeholder-category";
        placeholder.style.position = "absolute";
        placeholder.style.width = `${rect.width}px`;
        placeholder.style.height = `${rect.height}px`;
    } else {
        placeholder.className = "dnd-placeholder dnd-placeholder-card";
        // Копируем CSS-переменные (span карточки), чтобы placeholder совпадал по размеру.
        placeholder.style.cssText = source.style.cssText;
        placeholder.style.height = `${rect.height}px`;
    }

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
    clone.style.display = "";
    clone.style.width = `${rect.width}px`;
    clone.style.height = `${rect.height}px`;
    clone.style.pointerEvents = "none";
    clone.style.zIndex = "1000";
    clone.style.transition = "none";
    clone.style.transform = `translate(${rect.left}px, ${rect.top}px)`;
    return clone;
}

function movePlaceholder(container, placeholder, before) {
    if (placeholder.parentElement !== container) {
        container.insertBefore(placeholder, before);
        return;
    }

    if (placeholder.nextElementSibling !== before) {
        container.insertBefore(placeholder, before);
    }
}

function findCardContainer(x, y) {
    for (const container of document.querySelectorAll(".category-cards")) {
        const rect = container.getBoundingClientRect();
        if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom) {
            return container;
        }
    }

    // Курсор не попал точно в сетку карточек — используем категорию под курсором.
    const under = document.elementFromPoint(x, y);
    const category = under?.closest(".dashboard-category");
    return category ? category.querySelector(".category-cards") : null;
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

function collectCardOrder(container, placeholder, item) {
    const ids = [];

    for (const child of container.children) {
        if (child === item) {
            continue;
        }

        if (placeholder && child === placeholder) {
            const movedId = parseId(item.dataset.cardId);
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

function startSession(kind, item, container, event) {
    const rect = item.getBoundingClientRect();

    dragState = {
        kind,
        item,
        container,
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        rect,
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

function onPointerDown(event) {
    if (event.button !== 0 || syncInProgress || dragState) {
        return;
    }

    // Перетаскивание доступно только в режиме редактирования.
    if (!editModeEnabled) {
        return;
    }

    if (!(event.target instanceof Element)) {
        return;
    }

    const handle = event.target.closest(".drag-handle");
    if (handle) {
        const category = handle.closest(".dashboard-category");
        if (category) {
            startSession("category", category, category.parentElement, event);
            return;
        }
    }

    const card = event.target.closest(".dashboard-card");
    if (card) {
        const container = card.closest(".category-cards");
        if (container) {
            startSession("card", card, container, event);
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

    const placeholder = createPlaceholder(state.kind, state.item, rect);

    if (state.kind === "category" && grid) {
        state.sourceSpan = readSpan(state.item);
        state.sourceHeight = state.item.offsetHeight;
        grid.appendChild(placeholder);
    } else {
        state.item.parentElement.insertBefore(placeholder, state.item);
    }

    state.placeholder = placeholder;

    // Фантом создаём до скрытия источника, иначе он унаследует display:none.
    const dataAttribute = state.kind === "card" ? "data-card-id" : "data-category-id";
    const clone = createClone(state.item, rect, dataAttribute);
    document.body.appendChild(clone);
    state.clone = clone;

    // Исходный элемент убираем из потока: его место занимает placeholder.
    state.item.style.display = "none";

    document.body.classList.add("dnd-active");
    suppressClick = true;
    updateDrag(event);
}

function updateDrag(event) {
    const state = dragState;
    const x = event.clientX;
    const y = event.clientY;

    if (state.clone) {
        state.clone.style.transform = `translate(${x - state.offsetX}px, ${y - state.offsetY}px)`;
    }

    if (state.kind === "category") {
        updateCategoryTarget(x, y);
    } else {
        updateCardTarget(x, y);
    }
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

    const { kind, item, placeholder } = state;
    const grid = getGrid();

    if (kind === "category" && grid) {
        const orderedIds = state.previewIds ?? collectCategoryOrder(grid);

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

        if (dotNetRef) {
            enqueueSync(() => dotNetRef.invokeMethodAsync("OnCategoriesReordered", orderedIds));
        }

        setTimeout(() => {
            suppressClick = false;
        }, 0);

        return;
    }

    let task = null;

    if (kind === "card") {
        const sourceContainer = state.container;
        const targetContainer = placeholder.parentElement;
        const sourceCategoryId = parseId(sourceContainer.dataset.categoryId);
        const targetCategoryId = parseId(targetContainer.dataset.categoryId);

        if (sourceCategoryId !== null && targetCategoryId !== null) {
            const targetIds = collectCardOrder(targetContainer, placeholder, item);
            const sourceIds = sourceContainer === targetContainer
                ? targetIds
                : collectCardOrder(sourceContainer, null, item);

            task = () => dotNetRef.invokeMethodAsync(
                "OnCardsReordered",
                sourceCategoryId,
                targetCategoryId,
                sourceIds,
                targetIds);
        }
    }

    cleanup();
    endSession();

    if (task && dotNetRef) {
        enqueueSync(task);
    }

    scheduleLayout();

    setTimeout(() => {
        suppressClick = false;
    }, 0);
}

function onPointerMove(event) {
    if (!dragState || event.pointerId !== dragState.pointerId) {
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

    cleanup();
    endSession();
}

function onClickCapture(event) {
    if (suppressClick) {
        event.preventDefault();
        event.stopImmediatePropagation();
        suppressClick = false;
    }
}

function onDragStart(event) {
    // У карточки нативная HTML-ссылка: браузер позволяет «потянуть» её
    // drag-and-drop и показывает фантом ссылки. Это нежелательно даже вне режима
    // редактирования, поэтому подавляем нативный dragstart всегда.
    event.preventDefault();
}

function onKeyDown(event) {
    if (event.key === "Escape" && dragState) {
        cleanup();
        endSession();
        scheduleLayout();
    }
}

function enqueueSync(task) {
    syncQueue.push(task);

    if (!syncInProgress) {
        void processSyncQueue();
    }
}

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
        console.error("Ошибка синхронизации порядка элементов dashboard", error);
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
    document.addEventListener("click", onClickCapture, true);
    document.addEventListener("dragstart", onDragStart, true);
    document.addEventListener("keydown", onKeyDown, true);

    const grid = getGrid();
    if (grid) {
        gridResizeObserver = new ResizeObserver(() => scheduleLayout());
        gridResizeObserver.observe(grid);

        itemResizeObserver = new ResizeObserver(() => scheduleLayout());

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
        scheduleLayout();
    }
}

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

    // Класс нужен CSS: захват курсора на элементах показывается только в режиме редактирования.
    document.documentElement.classList.toggle("dashboard-edit-mode", editModeEnabled);
}

export function disposeDragAndDrop() {
    document.removeEventListener("pointerdown", onPointerDown, true);
    document.removeEventListener("click", onClickCapture, true);
    document.removeEventListener("dragstart", onDragStart, true);
    document.removeEventListener("keydown", onKeyDown, true);

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

    cleanup();
    endSession();
    finalizeCategoryDrop();

    dotNetRef = null;
    syncQueue = [];
    syncInProgress = false;
    suppressClick = false;
}