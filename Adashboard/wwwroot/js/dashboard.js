let activeThemeMode = "auto";
let systemThemeMedia = null;
let categorySortable = null;
let cardSortables = [];
let syncQueue = [];
let syncInProgress = false;
let dragInProgress = false;

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

function disposeCards() {
    for (const sortable of cardSortables) {
        sortable.destroy();
    }

    cardSortables = [];
}

function parseId(value) {
    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) ? parsed : null;
}

// SortableJS перемещает DOM напрямую, а Blazor об этом не знает. Возвращаем перетаскиваемый
// элемент на исходную позицию, чтобы DOM снова совпадал с состоянием рендера Blazor,
// иначе последующая перерисовка может расставить элементы в неверном порядке.
function revertDragToOriginalPosition(event) {
    const item = event.item;
    const from = event.from;
    const oldIndex = event.oldIndex;

    if (!item || !from || typeof oldIndex !== "number") {
        return;
    }

    // Исключаем сам элемент, иначе индексы после его удаления смещаются и позиция восстанавливается неверно.
    const siblings = Array.from(from.children).filter((child) => child !== item);
    const referenceNode = siblings[oldIndex] ?? null;
    from.insertBefore(item, referenceNode);
}

function setSortablesDisabled(disabled) {
    if (categorySortable) {
        categorySortable.option("disabled", disabled);
    }

    for (const sortable of cardSortables) {
        sortable.option("disabled", disabled);
    }
}

function enqueueSync(workItem) {
    syncQueue.push(workItem);

    if (!syncInProgress) {
        void processSyncQueue();
    }
}

async function processSyncQueue() {
    syncInProgress = true;
    setSortablesDisabled(true);

    try {
        while (syncQueue.length > 0) {
            const next = syncQueue.shift();
            if (!next) {
                continue;
            }

            await next();
        }
    } catch (error) {
        console.error("Ошибка синхронизации сортировки с сервером", error);
    } finally {
        setSortablesDisabled(false);
        syncInProgress = false;

        if (syncQueue.length > 0) {
            void processSyncQueue();
        }
    }
}

export function initializeSortable(dotNetRef) {
    disposeSortable();

    const grid = document.getElementById("dashboard-grid");
    if (!grid || typeof Sortable === "undefined") {
        return;
    }

    categorySortable = new Sortable(grid, {
        animation: 180,
        handle: ".drag-handle",
        draggable: ".dashboard-category",
        ghostClass: "sortable-ghost",
        chosenClass: "sortable-chosen",
        onStart: () => {
            dragInProgress = true;
        },
        onEnd: (event) => {
            if (!dragInProgress) {
                return;
            }

            dragInProgress = false;

            const orderedIds = Array.from(grid.querySelectorAll(":scope > .dashboard-category"))
                .map((element) => parseId(element.dataset.categoryId))
                .filter((id) => id !== null);

            revertDragToOriginalPosition(event);

            enqueueSync(() => dotNetRef.invokeMethodAsync("OnCategoriesReordered", orderedIds));
        }
    });

    const cardContainers = grid.querySelectorAll(".category-cards");
    for (const container of cardContainers) {
        const sortable = new Sortable(container, {
            group: "dashboard-cards",
            animation: 160,
            draggable: ".dashboard-card",
            ghostClass: "sortable-ghost",
            chosenClass: "sortable-chosen",
            onStart: () => {
                dragInProgress = true;
            },
            onEnd: (event) => {
                if (!dragInProgress) {
                    return;
                }

                dragInProgress = false;

                const source = event.from;
                const target = event.to;
                const sourceCategoryId = parseId(source.dataset.categoryId);
                const targetCategoryId = parseId(target.dataset.categoryId);

                if (sourceCategoryId === null || targetCategoryId === null) {
                    return;
                }

                const sourceCardIds = Array.from(source.querySelectorAll(":scope > .dashboard-card"))
                    .map((element) => parseId(element.dataset.cardId))
                    .filter((id) => id !== null);

                const targetCardIds = source === target
                    ? sourceCardIds
                    : Array.from(target.querySelectorAll(":scope > .dashboard-card"))
                        .map((element) => parseId(element.dataset.cardId))
                        .filter((id) => id !== null);

                revertDragToOriginalPosition(event);

                enqueueSync(() => dotNetRef.invokeMethodAsync(
                    "OnCardsReordered",
                    sourceCategoryId,
                    targetCategoryId,
                    sourceCardIds,
                    targetCardIds));
            }
        });

        cardSortables.push(sortable);
    }
}

export function disposeSortable() {
    syncQueue = [];
    syncInProgress = false;
    dragInProgress = false;

    if (categorySortable) {
        categorySortable.destroy();
        categorySortable = null;
    }

    disposeCards();
}
