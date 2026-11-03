import "./styles.css";
import { ApiClient, errorText } from "./api";
import { find } from "./dom";
import { normalizeServerUrl, readSettings, saveSettings, serverUrlError } from "./storage";
import type { ExtensionSettings } from "./types";

/** Элементы страницы настроек: собираются один раз, дальше меняются только значения и видимость. */
const ui = {
    status: find("status"),
    error: find("error"),
    form: find<HTMLFormElement>("settings-form"),
    serverUrlInput: find<HTMLInputElement>("server-url-input"),
    apiKeyInput: find<HTMLInputElement>("api-key-input"),
    saveButton: find<HTMLButtonElement>("save-button"),
    checkButton: find<HTMLButtonElement>("check-button"),
};

/**
 * Точка входа страницы настроек: сохранённые значения читаются один раз при открытии,
 * дальше сохранение и проверка подключения выполняются только по действию пользователя.
 */
async function start(): Promise<void> {
    ui.form.addEventListener("submit", (event) => {
        event.preventDefault();
        void save();
    });

    ui.checkButton.addEventListener("click", () => void check());

    // Ошибка предыдущей проверки неактуальна, когда пользователь исправляет адрес.
    ui.serverUrlInput.addEventListener("input", () => hideError());

    try {
        const settings = await readSettings();
        if (!settings) {
            setStatus("Настройки не заданы: заполните поля и сохраните.", false);
            return;
        }

        ui.serverUrlInput.value = settings.serverUrl;
        ui.apiKeyInput.value = settings.apiKey;
        setStatus(`Настройки загружены. Сервер: ${settings.serverUrl}`, false);
    } catch {
        // Хранилище недоступно: форму всё равно можно заполнить, но сохранить не выйдет.
        setStatus("", false);
        showError("Не удалось прочитать настройки из хранилища браузера.");
    }
}

/** Значения формы как настройки: адрес нормализуется, ключ обрезается, проверка идёт по тем же значениям. */
function formSettings(): ExtensionSettings {
    return { serverUrl: normalizeServerUrl(ui.serverUrlInput.value), apiKey: ui.apiKeyInput.value.trim() };
}

/** Сохраняет значения в storage.local; запросов к серверу при этом нет. */
async function save(): Promise<void> {
    const problem = serverUrlError(ui.serverUrlInput.value);
    if (problem) {
        showError(problem);
        return;
    }

    try {
        const settings = formSettings();
        await saveSettings(settings);
        setStatus(`Сохранено. Сервер: ${settings.serverUrl}`, false);
    } catch {
        showError("Не удалось сохранить настройки в хранилище браузера.");
    }
}

/** Проверяет подключение по значениям формы: GET /api/health/access с заголовком X-Api-Key. */
async function check(): Promise<void> {
    const problem = serverUrlError(ui.serverUrlInput.value);
    if (problem) {
        showError(problem);
        return;
    }

    setStatus("Проверяю подключение…", false);
    ui.checkButton.disabled = true;

    try {
        await new ApiClient(formSettings()).checkAccess();
        setStatus("Подключение успешно: сервер принял ключ доступа.", true);
    } catch (error) {
        // Текст отказа уже понятен: 401 — про ключ, сетевая ошибка — про подключение. Повтор — этим же нажатием.
        setStatus("", false);
        showError(errorText(error));
    } finally {
        ui.checkButton.disabled = false;
    }
}

/** Строка состояния: загрузка, сохранённые значения, ход проверки и её результат. */
function setStatus(message: string, success: boolean): void {
    ui.status.textContent = message;
    ui.status.hidden = message.length === 0;
    ui.status.classList.toggle("ok", success);
}

/** Ошибка валидации или отказа проверки; повтор — повторным нажатием кнопки проверки. */
function showError(message: string): void {
    ui.error.textContent = message;
    ui.error.hidden = false;
}

/** Убирает ошибку: она неактуальна, когда пользователь исправляет адрес сервера. */
function hideError(): void {
    ui.error.textContent = "";
    ui.error.hidden = true;
}

void start();
