import type { CardStatusMode, DashboardCard, HealthState } from './types';

// Проверка доступности сервиса карточки из браузера.
// Семантика режимов повторяет HealthCheckService из Blazor-проекта.

const REQUEST_TIMEOUT_MS = 5000;

/** Собирает абсолютный HTTP(S)-адрес карточки. Возвращает null, если адрес невалиден. */
function buildAbsoluteUrl(card: DashboardCard): string | null {
	const value = `${card.urlScheme}${card.url}`.trim();

	try {
		const parsed = new URL(value);
		if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
			return null;
		}

		if (parsed.hostname.length === 0) {
			return null;
		}

		return parsed.toString();
	} catch {
		return null;
	}
}

/** Определяет состояние сервиса по режиму проверки и коду ответа. */
export function evaluateStatus(mode: CardStatusMode, statusCode: number): HealthState {
	switch (mode) {
		case 'success':
			return statusCode >= 200 && statusCode < 300 ? 'online' : 'offline';
		case 'noServerError':
			return statusCode < 500 ? 'online' : 'offline';
		case 'reachable':
			return 'online';
		default:
			return 'unknown';
	}
}

/**
 * Выполняет одну проверку доступности карточки.
 * Сетевые ошибки и CORS дают состояние offline, отмена всего цикла пробрасывается дальше.
 */
export async function checkCardHealth(card: DashboardCard, signal?: AbortSignal): Promise<HealthState> {
	if (card.statusMode === 'none') {
		return 'unknown';
	}

	const url = buildAbsoluteUrl(card);
	if (url === null) {
		return 'unknown';
	}

	const controller = new AbortController();
	const onAbort = () => controller.abort();
	signal?.addEventListener('abort', onAbort, { once: true });
	const timer = window.setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

	try {
		const response = await fetch(url, {
			method: 'GET',
			redirect: 'follow',
			signal: controller.signal
		});

		return evaluateStatus(card.statusMode, response.status);
	} catch (error) {
		// Отмена всего цикла проверок — не ошибка карточки, пробрасываем её наверх.
		if (signal?.aborted) {
			throw error;
		}

		// Таймаут, сетевой сбой или блокировка CORS считаем недоступностью сервиса.
		return 'offline';
	} finally {
		window.clearTimeout(timer);
		signal?.removeEventListener('abort', onAbort);
	}
}
