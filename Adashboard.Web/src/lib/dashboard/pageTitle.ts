// Получение заголовка HTML-страницы из браузера.
// Повторяет поведение PageTitleService из Blazor-проекта.
// Браузер применяет политику CORS, поэтому часть ошибок ожидаема.

const REQUEST_TIMEOUT_MS = 8000;
const MAX_TITLE_LENGTH = 120;

/** Нормализует пробелы и обрезает слишком длинный заголовок. */
function normalizeTitle(raw: string): string | null {
	const title = raw.replace(/\s+/g, ' ').trim();
	if (title.length === 0) {
		return null;
	}

	return title.length > MAX_TITLE_LENGTH ? title.slice(0, MAX_TITLE_LENGTH) : title;
}

/**
 * Пытается получить содержимое тега <title> по указанному адресу.
 * Бросает InvalidOperationException-подобную ошибку (Error) с понятным текстом.
 */
export async function fetchPageTitle(url: string, signal?: AbortSignal): Promise<string | null> {
	const target = url.trim();

	let parsed: URL;
	try {
		parsed = new URL(target);
	} catch {
		throw new Error('Укажите корректную HTTP(S)-ссылку.');
	}

	if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:' || parsed.hostname.length === 0) {
		throw new Error('Укажите корректную HTTP(S)-ссылку.');
	}

	const controller = new AbortController();
	const onAbort = () => controller.abort();
	signal?.addEventListener('abort', onAbort, { once: true });
	const timer = window.setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

	try {
		const response = await fetch(parsed.toString(), {
			method: 'GET',
			redirect: 'follow',
			signal: controller.signal
		});

		if (!response.ok) {
			throw new Error(`Страница вернула ошибку (код ${response.status}).`);
		}

		const html = await response.text();
		const parsedDocument = new DOMParser().parseFromString(html, 'text/html');
		return normalizeTitle(parsedDocument.title);
	} catch (error) {
		if (signal?.aborted) {
			throw error;
		}

		if (error instanceof TypeError) {
			// Браузерный fetch бросает TypeError при сетевой ошибке или блокировке CORS.
			throw new Error('Не удалось подключиться к сервису (возможно, ограничение CORS).');
		}

		if (error instanceof DOMException && error.name === 'AbortError') {
			throw new Error('Страница не ответила за отведённое время.');
		}

		throw error;
	} finally {
		window.clearTimeout(timer);
		signal?.removeEventListener('abort', onAbort);
	}
}
