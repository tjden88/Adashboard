// Взаимодействие с Coloris: привязка палитры к конкретному input и обновление набора образцов.
const instances = new Map();

function isColorisAvailable() {
    return typeof window.Coloris !== "undefined";
}

/**
 * Инициализирует Coloris для конкретного поля и запоминает состояние привязки.
 * @param {string} inputId Идентификатор input.
 * @param {object} options Параметры Coloris (theme, alpha, swatches и т.д.).
 * @param {string} themeMode Режим темы: light, dark или auto.
 * @param {object} dotNetRef Ссылка на .NET-объект для обратного вызова.
 */
export function initializeColoris(inputId, options, themeMode, dotNetRef) {
    const input = document.getElementById(inputId);
    if (!input) {
        return;
    }

    if (!isColorisAvailable()) {
        console.warn("Coloris не загружен: поле выбора цвета работает как обычное текстовое поле.");
        return;
    }

    const instanceOptions = Object.assign({}, options, {
        themeMode: themeMode || "auto"
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

/**
 * Обновляет набор образцов цвета для поля.
 * @param {string} inputId Идентификатор input.
 * @param {string[]} swatches Новый набор цветов.
 */
export function updateSwatches(inputId, swatches) {
    const state = instances.get(inputId);
    if (!state || !isColorisAvailable()) {
        return;
    }

    state.options = Object.assign({}, state.options, { swatches: swatches || [] });
    Coloris.setInstance(`#${inputId}`, state.options);
}

/**
 * Освобождает ресурсы Coloris, связанные с полем.
 * @param {string} inputId Идентификатор input.
 */
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
