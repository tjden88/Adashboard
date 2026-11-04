import { browserApi } from "./browser";
import type { ExtensionSettings } from "./types";

/** Ключи storage.local: настройки расширения, запомненная страница и карта «pageId → categoryId». */
const SETTINGS_KEY = "settings";
const LAST_PAGE_KEY = "lastPageId";
const PAGE_CATEGORY_KEY = "pageCategory";

/** Форма сохранённых значений storage.local. Всё хранится локально: sync не используется, чтобы ключ не уходил на другие устройства. */
interface StorageValues {
    settings?: ExtensionSettings;
    lastPageId?: number;
    pageCategory?: Record<number, number>;
}

/** Настройки расширения; null — настроек нет, и интерфейс должен предложить их заполнить. */
export async function readSettings(): Promise<ExtensionSettings | null> {
    const stored = await browserApi.storage.local.get<StorageValues>([SETTINGS_KEY]);
    const settings = stored.settings;
    if (!settings) return null;

    const serverUrl = normalizeServerUrl(settings.serverUrl);
    // Значение могло быть сохранено раньше без проверки, поэтому схему подтверждаем перед использованием.
    return serverUrlError(serverUrl) === null ? { serverUrl, apiKey: settings.apiKey } : null;
}

/** Сохраняет настройки: адрес нормализуется, ключ остаётся только в этом браузере. */
export async function saveSettings(settings: ExtensionSettings): Promise<void> {
    await browserApi.storage.local.set<StorageValues>({
        settings: { serverUrl: normalizeServerUrl(settings.serverUrl), apiKey: settings.apiKey.trim() },
    });
}

/** Запомненная страница; null — выбор страницы ещё не делался, поэтому берётся первая по порядку. */
export async function readLastPageId(): Promise<number | null> {
    const stored = await browserApi.storage.local.get<StorageValues>([LAST_PAGE_KEY]);
    return stored.lastPageId ?? null;
}

/** Запоминает выбранную страницу. */
export async function saveLastPageId(pageId: number): Promise<void> {
    await browserApi.storage.local.set<StorageValues>({ lastPageId: pageId });
}

/** Запомненная категория страницы; null — выбор для этой страницы ещё не делался. */
export async function readCategoryId(pageId: number): Promise<number | null> {
    const stored = await browserApi.storage.local.get<StorageValues>([PAGE_CATEGORY_KEY]);
    return stored.pageCategory?.[pageId] ?? null;
}

/** Запоминает категорию страницы: выбор хранится отдельно для каждой страницы. */
export async function saveCategoryId(pageId: number, categoryId: number): Promise<void> {
    const stored = await browserApi.storage.local.get<StorageValues>([PAGE_CATEGORY_KEY]);
    const map = stored.pageCategory ?? {};
    map[pageId] = categoryId;
    await browserApi.storage.local.set<StorageValues>({ pageCategory: map });
}

/** Адрес сервера без завершающего слэша; пустая строка — адрес не задан. */
export function normalizeServerUrl(value: string): string {
    return value.trim().replace(/\/+$/, "");
}

/**
 * Проверка адреса сервера: схема только http или https, имя хоста обязательно.
 * Возвращает текст ошибки для интерфейса или null, когда адрес корректен.
 */
export function serverUrlError(value: string): string | null {
    const url = normalizeServerUrl(value);
    if (!url) return "Укажите адрес сервера.";

    let parsed: URL;
    try {
        parsed = new URL(url);
    } catch {
        return "Адрес сервера читается только как http://адрес или https://адрес.";
    }

    // Значение без схемы браузер читает как другой протокол («localhost:8080»), поэтому схему проверяем отдельно.
    if (parsed.protocol !== "http:" && parsed.protocol !== "https:") return "Адрес сервера работает только по http:// или https://.";

    if (!parsed.hostname) return "В адресе не указано имя сервера.";

    // Путь-префикс допустим (сервер может работать под ним), а параметры и якорь ломают адреса запросов.
    if (parsed.search || parsed.hash) return "Указывайте базовый адрес сервера, без параметров и якоря.";

    return null;
}
