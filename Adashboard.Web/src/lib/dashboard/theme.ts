import type { ThemeMode } from './types';

/** Разрешает режим темы в конкретное значение с учётом системной настройки. */
export function resolveTheme(mode: ThemeMode): 'dark' | 'light' {
	if (mode === 'light' || mode === 'dark') {
		return mode;
	}

	return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

/** Применяет тему к документу через атрибут data-theme на <html>. */
export function applyTheme(mode: ThemeMode): void {
	document.documentElement.setAttribute('data-theme', resolveTheme(mode));
}

/**
 * Подписывается на изменение системной темы.
 * Возвращает функцию отписки.
 */
export function watchSystemTheme(onChange: () => void): () => void {
	const media = window.matchMedia('(prefers-color-scheme: dark)');
	media.addEventListener('change', onChange);
	return () => media.removeEventListener('change', onChange);
}
