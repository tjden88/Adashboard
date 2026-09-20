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

// Собственный drag-and-drop на Pointer Events.
// Важно: во время перетаскивания порядок списков не меняется — двигается только
// placeholder. Итоговый порядок Blazor применяет сам, поэтому DOM всегда согласован.
const DRAG_THRESHOLD_PX = 6;

let dotNetRef = null;
let dragState = null;
let suppressClick = false;
let syncQueue = [];
let syncInProgress = false;

function getGrid() {
    return document.getElementById("dashboard-grid");
}

function parseId(value) {
    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) ? parsed : null;
}

// Определяет, перед каким элементом вставить placeholder. Работает и для строк,
// и для сетки с переносом: сначала сравниваем по строке, затем по горизонтали.
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

function createPlaceholder(kind, source, rect) {
    const placeholder = document.createElement("div");
    placeholder.className = kind === "category"
        ? "dnd-placeholder dnd-placeholder-category"
        : "dnd-placeholder dnd-placeholder-card";

    // Копируем CSS-переменные (span/colspan), чтобы placeholder совпадал по размеру.
    placeholder.style.cssText = source.style.cssText;

    if (kind === "category") {
        placeholder.style.minHeight = `${rect.height}px`;
    } else {
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

    const categories = Array.from(grid.children).filter(
        (child) => child.classList.contains("dashboard-category") && child !== state.item);

    movePlaceholder(grid, state.placeholder, findInsertBefore(categories, x, y));
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

function collectCategoryOrder(grid, placeholder, item) {
    const ids = [];

    for (const child of grid.children) {
        if (child === item) {
            continue;
        }

        if (child === placeholder) {
            const movedId = parseId(item.dataset.categoryId);
            if (movedId !== null) {
                ids.push(movedId);
            }
            continue;
        }

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
        active: false
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

    state.active = true;
    state.offsetX = event.clientX - rect.left;
    state.offsetY = event.clientY - rect.top;

    const placeholder = createPlaceholder(state.kind, state.item, rect);
    state.item.parentElement.insertBefore(placeholder, state.item);
    state.placeholder = placeholder;

    // Фантом создаём до скрытия источника, иначе он унаследует display:none.
    const dataAttribute = state.kind === "card" ? "data-card-id" : "data-category-id";
    const clone = createClone(state.item, rect, dataAttribute);
    document.body.appendChild(clone);
    state.clone = clone;

    // Исходный элемент убираем из потока: его место занимает placeholder,
    // а за курсором следует полупрозрачный фантом.
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

function finishDrag() {
    const state = dragState;
    if (!state) {
        return;
    }

    const { kind, item, placeholder } = state;
    const grid = getGrid();
    let task = null;

    if (kind === "category" && grid) {
        const orderedIds = collectCategoryOrder(grid, placeholder, item);
        task = () => dotNetRef.invokeMethodAsync("OnCategoriesReordered", orderedIds);
    } else if (kind === "card") {
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
    if (dragState) {
        event.preventDefault();
    }
}

function onKeyDown(event) {
    if (event.key === "Escape" && dragState) {
        cleanup();
        endSession();
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
}

export function disposeDragAndDrop() {
    document.removeEventListener("pointerdown", onPointerDown, true);
    document.removeEventListener("click", onClickCapture, true);
    document.removeEventListener("dragstart", onDragStart, true);
    document.removeEventListener("keydown", onKeyDown, true);

    cleanup();
    endSession();

    dotNetRef = null;
    syncQueue = [];
    syncInProgress = false;
    suppressClick = false;
}