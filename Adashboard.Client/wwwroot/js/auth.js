// Хранение ключа доступа к API в localStorage браузера.
//
// Ключ не должен попадать в исходники клиента (SPA публичен), поэтому пользователь вводит
// его сам, а браузер запоминает значение между запусками. Модуль подключает ApiKeyStore.

const STORAGE_KEY = "adashboard-api-key";

export function getApiKey() {
    try {
        return window.localStorage.getItem(STORAGE_KEY) || "";
    } catch (error) {
        // Приватный режим или отключённое хранилище: ключ не читается.
        return "";
    }
}

export function setApiKey(value) {
    try {
        window.localStorage.setItem(STORAGE_KEY, value);
    } catch (error) {
        // Ключ не сохранится между запусками, но текущий сеанс продолжит работать.
    }
}

export function clearApiKey() {
    try {
        window.localStorage.removeItem(STORAGE_KEY);
    } catch (error) {
    }
}
