// Расчёт контрастности цветов карточек.
// Перенос логики DashboardCard.ShouldUseLightText из Blazor-проекта.

interface Rgb {
	r: number;
	g: number;
	b: number;
}

/** Разбирает цвет #RRGGBB; возвращает null, если строка некорректна. */
function parseHexColor(hex: string): Rgb | null {
	if (!hex) {
		return null;
	}

	const raw = hex.trim().replace(/^#/, '');
	if (raw.length !== 6 || !/^[0-9a-fA-F]{6}$/.test(raw)) {
		return null;
	}

	return {
		r: Number.parseInt(raw.slice(0, 2), 16),
		g: Number.parseInt(raw.slice(2, 4), 16),
		b: Number.parseInt(raw.slice(4, 6), 16)
	};
}

/** Линеаризация канала по формуле WCAG. */
function linearize(channel: number): number {
	return channel <= 0.04045 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
}

/** Определяет, нужен ли для данного фона светлый (белый) текст и иконка. */
export function shouldUseLightText(hexColor: string): boolean {
	const rgb = parseHexColor(hexColor);
	if (!rgb) {
		return true;
	}

	const r = linearize(rgb.r / 255);
	const g = linearize(rgb.g / 255);
	const b = linearize(rgb.b / 255);
	const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

	return luminance < 0.45;
}

/** Цвет иконки карточки, подобранный по фону. */
export function resolveCardIconColor(backgroundColor: string): string {
	return shouldUseLightText(backgroundColor) ? '#ffffff' : '#0f172a';
}

/** Цвет текста карточки, подобранный по фону. */
export function resolveCardTextColor(backgroundColor: string): string {
	return shouldUseLightText(backgroundColor) ? '#ffffff' : '#0f172a';
}

/** Цвет фона по умолчанию, подобранный по ключевому слову иконки. */
export function resolveDefaultBackgroundColor(iconClass: string): string {
	const icon = iconClass.toLowerCase();

	if (icon.includes('database')) {
		return '#b91c1c';
	}

	if (icon.includes('chart')) {
		return '#ea580c';
	}

	if (icon.includes('docker')) {
		return '#0369a1';
	}

	if (icon.includes('heart')) {
		return '#15803d';
	}

	return '#1e40af';
}
