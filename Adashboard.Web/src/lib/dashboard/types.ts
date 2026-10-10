// Модели данных dashboard.
// Полностью повторяют структуру старого Blazor-проекта, только на TypeScript.

/** Режим проверки доступности сервиса, на который ведёт карточка. */
export type CardStatusMode = 'none' | 'reachable' | 'noServerError' | 'success';

/** Состояние доступности сервиса, определённое последней проверкой. */
export type HealthState = 'unknown' | 'online' | 'offline';

/** Режим темы оформления. */
export type ThemeMode = 'auto' | 'dark' | 'light';

/** Карточка ссылки внутри категории. */
export interface DashboardCard {
	/** Стабильный идентификатор карточки. */
	id: number;
	/** Идентификатор категории, которой принадлежит карточка. */
	categoryId: number;
	/** Отображаемое название. */
	title: string;
	/** Адрес перехода (может содержать схему или храниться без неё). */
	url: string;
	/** Схема, добавляемая к адресу при открытии: "http://", "https://" или пустая строка. */
	urlScheme: string;
	/** CSS-класс иконки Font Awesome. */
	iconClass: string;
	/** Ссылка или data URL собственного изображения-иконки (пустая строка — иконка не задана). */
	imagePath: string;
	/** Цвет фона карточки в формате #RRGGBB. */
	backgroundColor: string;
	/** Признак широкой карточки: занимает две колонки вместо одной. */
	isWide: boolean;
	/** Режим проверки доступности. */
	statusMode: CardStatusMode;
	/** Порядок карточки внутри категории. */
	order: number;
}

/** Категория ссылок. */
export interface DashboardCategory {
	id: number;
	/** Отображаемое название. */
	title: string;
	/** Ширина категории в колонках (1–12). */
	width: number;
	/** Порядок категории среди остальных. */
	order: number;
	/** Карточки категории. */
	cards: DashboardCard[];
}

/** Значения настроек dashboard. */
export interface SiteSettings {
	themeMode: ThemeMode;
	siteTitle: string;
	siteDescription: string;
	showLogo: boolean;
	healthCheckIntervalSeconds: number;
}

/** Корневая модель раскладки. */
export interface DashboardLayout extends SiteSettings {
	id: number;
	categories: DashboardCategory[];
}

// Ограничения, повторяющие правила старого приложения.
export const MIN_HEALTH_INTERVAL_SECONDS = 10;
export const MAX_HEALTH_INTERVAL_SECONDS = 600;
export const DEFAULT_HEALTH_INTERVAL_SECONDS = 30;
export const MIN_CATEGORY_WIDTH = 1;
export const MAX_CATEGORY_WIDTH = 12;
export const DEFAULT_CATEGORY_WIDTH = 4;
export const DEFAULT_CARD_COLOR = '#1e40af';
export const DEFAULT_CARD_ICON = 'fa-solid fa-link';
export const MAX_CONCURRENT_HEALTH_CHECKS = 6;
