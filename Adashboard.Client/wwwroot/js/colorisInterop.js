// Взаимодействие с Coloris: привязка палитры к конкретному полю выбора цвета и обновление
// набора образцов. Сам выбор цвета рисует библиотека, поэтому Blazor нужен только мост.

const instances = new Map();

function isColorisAvailable() {
    return typeof window.Coloris !== "undefined";
}

// Тему палитры берём из оформления приложения (атрибут data-theme на <html>), а не из
// системных настроек: при явно выбранной теме палитра иначе выпадала бы из оформления.
function resolveThemeMode() {
    const theme = document.documentElement.getAttribute("data-theme");
    return theme === "dark" || theme === "light" ? theme : "auto";
}

// Инициализирует Coloris для конкретного поля и запоминает состояние привязки.
export function initializeColoris(inputId, options, dotNetRef) {
    const input = document.getElementById(inputId);
    if (!input) {
        return;
    }

    if (!isColorisAvailable()) {
        console.warn("Coloris не загружен: поле выбора цвета работает как обычное текстовое поле.");
        return;
    }

    const instanceOptions = Object.assign({}, options, {
        themeMode: resolveThemeMode()
    });

    const onChanged = () => {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnColorPicked", input.value);
        }
    };

    input.addEventListener("change", onChanged);

    instances.set(inputId, {
        options: instanceOptions,
        onChanged
    });

    Coloris.setInstance(`#${inputId}`, instanceOptions);
}

// Обновляет набор образцов цвета у поля.
export function updateSwatches(inputId, swatches) {
    const state = instances.get(inputId);
    if (!state || !isColorisAvailable()) {
        return;
    }

    state.options = Object.assign({}, state.options, { swatches: swatches || [] });
    Coloris.setInstance(`#${inputId}`, state.options);
}

// Освобождает ресурсы Coloris, связанные с полем.
export function disposeColoris(inputId) {
    const state = instances.get(inputId);
    if (!state) {
        return;
    }

    const input = document.getElementById(inputId);
    if (input && state.onChanged) {
        input.removeEventListener("change", state.onChanged);
    }

    if (isColorisAvailable()) {
        Coloris.removeInstance(`#${inputId}`);
    }

    instances.delete(inputId);
}
