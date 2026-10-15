// Оформление dashboard: режим темы и фоновые изображения страницы.
// Модуль подключает MainLayout; порядок категорий и карточек добавит часть drag-and-drop.

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
