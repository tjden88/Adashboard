// Работа со ссылками карточек: разбор и проверка схемы.
// Повторяет логику CardDialog из Blazor-проекта.

/** Выделяет схему из адреса, если она там уже указана. */
export function splitScheme(url: string): { scheme: string; url: string } {
	if (url.toLowerCase().startsWith('http://')) {
		return { scheme: 'http://', url: url.slice(7) };
	}

	if (url.toLowerCase().startsWith('https://')) {
		return { scheme: 'https://', url: url.slice(8) };
	}

	return { scheme: '', url };
}

/** Нормализует адрес: схема внутри адреса имеет приоритет над переключателем. */
export function normalizeUrl(scheme: string, rawUrl: string): { scheme: string; url: string } {
	const trimmed = rawUrl.trim();
	const split = splitScheme(trimmed);
	return split.scheme.length > 0 ? split : { scheme, url: trimmed };
}

/** Запрещает опасные схемы, если адрес задаётся без префикса. */
export function isSafeUrl(scheme: string, url: string): boolean {
	if (scheme.length > 0) {
		return true;
	}

	const normalized = url.trim().toLowerCase();
	const dangerous = ['javascript:', 'data:', 'vbscript:', 'file:', 'blob:'];

	return !dangerous.some((prefix) => normalized.startsWith(prefix));
}

/** Проверяет, что цвет задан в формате #RRGGBB. */
export function isValidHexColor(color: string): boolean {
	return /^#[0-9a-fA-F]{6}$/.test(color);
}
