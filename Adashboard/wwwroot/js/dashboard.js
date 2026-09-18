let activeThemeMode = "auto";
let systemThemeMedia = null;
let categorySortable = null;
let cardSortables = [];

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
        onEnd: () => {
            const orderedIds = Array.from(grid.querySelectorAll(":scope > .dashboard-category"))
                .map((element) => parseId(element.dataset.categoryId))
                .filter((id) => id !== null);

            dotNetRef.invokeMethodAsync("OnCategoriesReordered", orderedIds);
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
            onEnd: (event) => {
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

                dotNetRef.invokeMethodAsync(
                    "OnCardsReordered",
                    sourceCategoryId,
                    targetCategoryId,
                    sourceCardIds,
                    targetCardIds);
            }
        });

        cardSortables.push(sortable);
    }
}

export function disposeSortable() {
    if (categorySortable) {
        categorySortable.destroy();
        categorySortable = null;
    }

    disposeCards();
}
