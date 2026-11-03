import { UrlScheme } from "./types";
import { CARD_TITLE_MAX_LENGTH, CARD_URL_MAX_LENGTH } from "./constants";

/** Адрес вкладки, разложенный как поля карточки: схема хранится отдельно, остальное — без http:// и https://. */
export interface CardUrlParts {
    scheme: UrlScheme;
    /** Адрес без схемы: ровно то, что хранится в поле url карточки. */
    rest: string;
    /** Имя хоста — подстановка для заголовка, когда заголовок вкладки пустой. */
    host: string;
}

/** Разбирает адрес вкладки; null — схема http или https, страницу с другой схемой добавить нельзя. */
export function parseCardUrl(rawUrl: string | undefined): CardUrlParts | null {
    if (!rawUrl) return null;

    let url: URL;
    try {
        url = new URL(rawUrl);
    } catch {
        // Вкладки расширения и служебные страницы браузера не проходят проверку схемы.
        return null;
    }

    if (url.protocol !== "http:" && url.protocol !== "https:") return null;

    return {
        scheme: url.protocol === "https:" ? UrlScheme.Https : UrlScheme.Http,
        rest: url.href.slice(url.protocol.length + 2),
        host: url.hostname,
    };
}

/** Заголовок новой карточки: заголовок вкладки, иначе имя хоста; длина — как в домене. */
export function cardTitle(tabTitle: string | undefined, host: string): string {
    const title = (tabTitle ?? "").trim();
    return (title || host).slice(0, CARD_TITLE_MAX_LENGTH);
}

/** Адрес помещается в поле карточки: проверка на границе, до запроса к API. */
export function isUrlLengthValid(value: string): boolean {
    return value.length <= CARD_URL_MAX_LENGTH;
}
