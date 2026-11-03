/**
 * Цвет фона новой карточки: читаем meta[name="theme-color"] активной вкладки через
 * executeScript (разрешение scripting), нормализуем формат и, если цвет получить не удалось,
 * используем значение по умолчанию из домена.
 */

import { browserApi } from "./browser";
import { DEFAULT_CARD_BACKGROUND_COLOR } from "./constants";

/** Формат, который принимает карточка: короткая или полная hex-запись цвета. */
const HEX_COLOR = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/;

/**
 * Нормализация theme-color: принимаем только #rgb и #rrggbb, #rgb расширяем до #rrggbb
 * и приводим к нижнему регистру — так значение совпадает с тем, что хранится в карточке.
 * Любой другой формат (named color, rgb(), пустая строка) — null, и берётся цвет по умолчанию.
 */
export function normalizeThemeColor(value: string | null): string | null {
    const color = (value ?? "").trim().toLowerCase();
    if (!HEX_COLOR.test(color)) return null;

    // #rgb расширяем по одной цифре на канал: #abc -> #aabbcc.
    return color.length === 4 ? `#${color[1]}${color[1]}${color[2]}${color[2]}${color[3]}${color[3]}` : color;
}

/**
 * Цвет фона карточки для активной вкладки. Значение читают один раз при открытии попапа,
 * поэтому сохранение не зависит от того, выполнится ли скрипт на странице.
 */
export async function cardBackgroundColor(tabId: number | null): Promise<string> {
    return normalizeThemeColor(await readThemeColor(tabId)) ?? DEFAULT_CARD_BACKGROUND_COLOR;
}

/** Значение theme-color вкладки; null — вкладка без идентификатора, мета отсутствует или скрипт не выполнился. */
async function readThemeColor(tabId: number | null): Promise<string | null> {
    if (tabId === null) return null;

    try {
        const results = await browserApi.scripting.executeScript({
            target: { tabId },
            func: () => document.querySelector('meta[name="theme-color"]')?.getAttribute("content") ?? null,
        });

        return results[0]?.result ?? null;
    } catch {
        // Страницы без права на executeScript (служебные страницы браузера, ограниченные вкладки):
        // это ожидаемый сценарий, поэтому в интерфейсе ошибку не показываем.
        return null;
    }
}
