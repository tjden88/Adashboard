// Оформление dashboard: режим темы, фоновые изображения страницы и раскладка категорий.
// Модуль оформления подключает MainLayout, раскладку — PageView; перетаскивание категорий
// и карточек добавит часть drag-and-drop.

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
// Раскладка категорий по колонкам (masonry).
//
// Категории позиционируются абсолютно: модуль измеряет их высоты и укладывает по колонкам
// сетки, подбирая каждой самое верхнее подходящее место. Число колонок зависит от ширины
// экрана (12 / 6 / 4), ширину категории задают её CSS-переменные --span-* (см. Category).
// До вызова initializeLayout категории укладывает CSS-grid, поэтому при недоступном модуле
// страница остаётся рабочей.
// ---------------------------------------------------------------------------

const GRID_ELEMENT_ID = "dashboard-grid";

let gridResizeObserver = null;
let itemResizeObserver = null;
let gridMutationObserver = null;
let layoutFrame = 0;
const observedCategories = new Set();

function getGrid() {
    return document.getElementById(GRID_ELEMENT_ID);
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
        (child) => child.classList.contains("dashboard-category"));
}

// Каждому элементу подбираем самое верхнее место, где помещается его ширина, не выходя
// за уже занятые области колонок.
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

    // Ширина сетки меняется при ресайзе и повороте экрана — раскладываем заново.
    gridResizeObserver = new ResizeObserver(() => scheduleLayout());
    gridResizeObserver.observe(grid);

    // Высота категории меняется от содержимого (например, добавили ссылку) — тоже перекладываем.
    itemResizeObserver = new ResizeObserver(() => scheduleLayout());

    // Появление и удаление категорий: раскладываем и заводим наблюдение за новыми.
    gridMutationObserver = new MutationObserver(() => {
        syncObservedCategories();
        scheduleLayout();
    });
    gridMutationObserver.observe(grid, { childList: true });

    syncObservedCategories();
    scheduleLayout();
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
