import type {
    DashboardCard,
    DashboardCategory,
    DashboardPage,
    DashboardPageReadModel,
    ExtensionSettings,
    SaveCardRequest,
} from "./types";

/** Имя HTTP-заголовка с ключом доступа, как в DomainConstants.ApiKeyHeaderName. */
const API_KEY_HEADER = "X-Api-Key";

/** Таймаут запроса к API: попап не должен ждать ответ дольше этого времени. */
export const REQUEST_TIMEOUT_MS = 10_000;

/** Ошибка обращения к API: текст понятен пользователю, без технических деталей. */
export class ApiError extends Error {
    /** HTTP-код ответа; null — ответа не было (сетевая ошибка, таймаут или отмена). */
    readonly status: number | null;

    constructor(message: string, status: number | null = null) {
        super(message);
        this.name = "ApiError";
        this.status = status;
    }
}

/** Текст ошибки для интерфейса: у ApiError он уже понятен пользователю, используется и в попапе, и в настройках. */
export function errorText(error: unknown): string {
    return error instanceof ApiError ? error.message : "Не удалось выполнить запрос. Попробуйте ещё раз.";
}

/** Клиент API Adashboard: каждый запрос идёт с заголовком X-Api-Key. */
export class ApiClient {
    private readonly settings: ExtensionSettings;

    constructor(settings: ExtensionSettings) {
        this.settings = settings;
    }

    /** Список страниц dashboard. */
    async getPages(signal?: AbortSignal): Promise<DashboardPage[]> {
        return this.json<DashboardPage[]>("api/pages", undefined, signal);
    }

    /** Страница вместе с категориями и карточками. */
    async getPage(pageId: number, signal?: AbortSignal): Promise<DashboardPageReadModel> {
        return this.json<DashboardPageReadModel>(`api/pages/${pageId}`, undefined, signal);
    }

    /** Создаёт категорию: успешный ответ — 201 и созданная категория. */
    async createCategory(category: DashboardCategory, signal?: AbortSignal): Promise<DashboardCategory> {
        return this.json<DashboardCategory>("api/categories", post(category), signal);
    }

    /** Создаёт карточку: успешный ответ — 201 и созданная карточка. */
    async createCard(request: SaveCardRequest, signal?: AbortSignal): Promise<DashboardCard> {
        return this.json<DashboardCard>("api/cards", post(request), signal);
    }

    /** Data URL иконки страницы: ответ читается как text/plain, а не как JSON. */
    async getIconDataUrl(url: string, signal?: AbortSignal): Promise<string> {
        return this.text(`api/probe/icon?url=${encodeURIComponent(url)}`, undefined, signal);
    }

    /** Проверяет ключ доступа: 200 без тела означает, что сервер ключ принял. */
    async checkAccess(signal?: AbortSignal): Promise<void> {
        await this.send("api/health/access", undefined, signal);
    }

    private async json<T>(path: string, init: RequestInit | undefined, signal?: AbortSignal): Promise<T> {
        const response = await this.send(path, init, signal);
        return (await response.json()) as T;
    }

    private async text(path: string, init: RequestInit | undefined, signal?: AbortSignal): Promise<string> {
        const response = await this.send(path, init, signal);
        return (await response.text()).trim();
    }

    /** Выполняет запрос и превращает неудачный ответ в ApiError с текстом из тела. */
    private async send(path: string, init: RequestInit | undefined, signal?: AbortSignal): Promise<Response> {
        const timeout = AbortSignal.timeout(REQUEST_TIMEOUT_MS);
        // fetch принимает только один сигнал, поэтому отмену вызывающего кода объединяем с таймаутом.
        const activeSignal = signal ? AbortSignal.any([signal, timeout]) : timeout;

        let response: Response;
        try {
            response = await fetch(joinUrl(this.settings.serverUrl, path), {
                ...init,
                headers: { [API_KEY_HEADER]: this.settings.apiKey, ...(init?.headers ?? {}) },
                signal: activeSignal,
            });
        } catch {
            // Причина отказа понятна по сигналу: текст исключения ненадёжен, abort приходит как DOMException.
            if (signal?.aborted) throw new ApiError("Запрос отменён.");
            if (timeout.aborted) throw new ApiError(`Сервер не ответил за ${Math.round(REQUEST_TIMEOUT_MS / 1000)} с.`);
            throw new ApiError("Не удалось подключиться к серверу.");
        }

        if (!response.ok) {
            // Текст отказа — то, что показывает интерфейс; на 401 важнее сообщение про ключ.
            const body = (await response.text()).trim();
            const message = response.status === 401 ? "Неверный ключ доступа." : body || `Сервер ответил ошибкой (${response.status}).`;
            throw new ApiError(message, response.status);
        }

        return response;
    }
}

/** Собирает адрес запроса относительно базового адреса сервера, сохраняя путь-префикс, если он есть. */
function joinUrl(baseUrl: string, path: string): string {
    return `${baseUrl.replace(/\/+$/, "")}/${path.replace(/^\/+/, "")}`;
}

/** Тело POST с JSON. */
function post(body: unknown): RequestInit {
    return { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) };
}
