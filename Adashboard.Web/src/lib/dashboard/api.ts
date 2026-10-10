// Связь с backend Adashboard.Api для чтения и сохранения раскладки dashboard.
//
// Формат API немного отличается от модели фронтенда, поэтому здесь есть два слоя:
//   1) «сырые» типы (Api*) — ровно то, что приходит из JSON от ASP.NET;
//   2) функции map* — переводят «сырой» формат в модель фронта и обратно.
//
// Отличия форматов:
//   - категория в API хранит position: { order, width }, во фронте — плоские order и width;
//   - карточка в API хранит position: { order }, во фронте — плоский order;
//   - statusMode в API — число (None = 0, Reachable = 1, NoServerError = 2, Success = 3),
//     во фронте — строковый union;
//   - категория в API хранит dashboardLayoutId, карточка — categoryId (при сохранении
//     заполняем их из контекста: из раскладки и из категории соответственно).
//
// Запросы идут напрямую на адрес backend Adashboard.Api (см. API_BASE_URL).
// Так как origin фронта и API различаются, браузер выполняет кросс-доменные
// запросы — на стороне API должен быть разрешён CORS для origin фронтенда.

import {
	DEFAULT_CATEGORY_WIDTH,
	type CardStatusMode,
	type DashboardCard,
	type DashboardCategory,
	type DashboardLayout,
	type ThemeMode
} from './types';

/** Базовый адрес backend Adashboard.Api (без завершающего слэша). */
const API_BASE_URL = 'http://localhost:5024';

/** Адрес эндпоинта раскладки (GET и PUT). */
const LAYOUT_ENDPOINT = `${API_BASE_URL}/api/layout`;

/** Позиция карточки в формате API. */
export interface ApiCardPosition {
	order: number;
}

/** Позиция категории в формате API. */
export interface ApiCategoryPosition {
	order: number;
	width: number;
}

/** Карточка в формате ответа API. */
export interface ApiDashboardCard {
	id: number;
	categoryId: number;
	title: string;
	url: string;
	urlScheme: string;
	iconClass: string;
	imagePath: string;
	backgroundColor: string;
	isWide: boolean;
	/** Числовой режим проверки статуса (0–3). */
	statusMode: number;
	position: ApiCardPosition;
}

/** Категория в формате ответа API. */
export interface ApiDashboardCategory {
	id: number;
	dashboardLayoutId: number;
	title: string;
	position: ApiCategoryPosition;
	cards: ApiDashboardCard[];
}

/** Корневая раскладка в формате ответа API. */
export interface ApiDashboardLayout {
	id: number;
	themeMode: string;
	siteTitle: string;
	siteDescription: string;
	showLogo: boolean;
	healthCheckIntervalSeconds: number;
	categories: ApiDashboardCategory[];
}

// Числовые значения enum CardStatusMode из backend.
const STATUS_MODE_NONE = 0;
const STATUS_MODE_REACHABLE = 1;
const STATUS_MODE_NO_SERVER_ERROR = 2;
const STATUS_MODE_SUCCESS = 3;

/** Строковый режим фронта → число для API. */
const STATUS_MODE_TO_API: Record<CardStatusMode, number> = {
	none: STATUS_MODE_NONE,
	reachable: STATUS_MODE_REACHABLE,
	noServerError: STATUS_MODE_NO_SERVER_ERROR,
	success: STATUS_MODE_SUCCESS
};

/** Переводит числовой статус из API в строковый режим фронта (неизвестное значение — 'none'). */
function statusModeFromApi(value: number): CardStatusMode {
	switch (value) {
		case STATUS_MODE_REACHABLE:
			return 'reachable';
		case STATUS_MODE_NO_SERVER_ERROR:
			return 'noServerError';
		case STATUS_MODE_SUCCESS:
			return 'success';
		default:
			return 'none';
	}
}

/** Переводит строковый статус фронта в число для API. */
function statusModeToApi(value: CardStatusMode): number {
	return STATUS_MODE_TO_API[value] ?? STATUS_MODE_NONE;
}

/** Переводит одну карточку из формата API в модель фронта. */
function mapCardFromApi(raw: ApiDashboardCard, categoryId: number): DashboardCard {
	return {
		id: raw.id,
		// categoryId берём из контекста категории: API хранит его в карточке,
		// но при вложенности он совпадает с id категории.
		categoryId: raw.categoryId ?? categoryId,
		title: raw.title ?? '',
		url: raw.url ?? '',
		urlScheme: raw.urlScheme ?? '',
		iconClass: raw.iconClass ?? '',
		imagePath: raw.imagePath ?? '',
		backgroundColor: raw.backgroundColor ?? '',
		isWide: Boolean(raw.isWide),
		statusMode: statusModeFromApi(raw.statusMode),
		order: raw.position?.order ?? 0
	};
}

/** Переводит одну категорию из формата API в модель фронта. */
function mapCategoryFromApi(raw: ApiDashboardCategory): DashboardCategory {
	const cards = (raw.cards ?? [])
		.map((card) => mapCardFromApi(card, raw.id))
		// Сортируем по position.order: API не гарантирует порядок элементов в JSON.
		.sort((a, b) => a.order - b.order);

	return {
		id: raw.id,
		title: raw.title ?? '',
		width: raw.position?.width ?? DEFAULT_CATEGORY_WIDTH,
		order: raw.position?.order ?? 0,
		cards
	};
}

/** Переводит всю раскладку из формата API в модель фронта. */
export function mapLayoutFromApi(raw: ApiDashboardLayout): DashboardLayout {
	const categories = (raw.categories ?? [])
		.map(mapCategoryFromApi)
		.sort((a, b) => a.order - b.order);

	return {
		id: raw.id,
		// Приводим строку к ожидаемому union; некорректные значения дальше исправит normalize().
		themeMode: raw.themeMode as ThemeMode,
		siteTitle: raw.siteTitle ?? '',
		siteDescription: raw.siteDescription ?? '',
		showLogo: Boolean(raw.showLogo),
		healthCheckIntervalSeconds: raw.healthCheckIntervalSeconds ?? 0,
		categories
	};
}

/** Переводит одну карточку из модели фронта в формат API. */
function mapCardToApi(card: DashboardCard, categoryId: number): ApiDashboardCard {
	return {
		id: card.id,
		categoryId,
		title: card.title,
		url: card.url,
		urlScheme: card.urlScheme,
		iconClass: card.iconClass,
		imagePath: card.imagePath,
		backgroundColor: card.backgroundColor,
		isWide: card.isWide,
		statusMode: statusModeToApi(card.statusMode),
		position: { order: card.order }
	};
}

/** Переводит одну категорию из модели фронта в формат API. */
function mapCategoryToApi(category: DashboardCategory, layoutId: number): ApiDashboardCategory {
	return {
		id: category.id,
		dashboardLayoutId: layoutId,
		title: category.title,
		position: { order: category.order, width: category.width },
		cards: category.cards.map((card) => mapCardToApi(card, category.id))
	};
}

/** Переводит всю раскладку из модели фронта в формат API. */
export function mapLayoutToApi(layout: DashboardLayout): ApiDashboardLayout {
	return {
		id: layout.id,
		themeMode: layout.themeMode,
		siteTitle: layout.siteTitle,
		siteDescription: layout.siteDescription,
		showLogo: layout.showLogo,
		healthCheckIntervalSeconds: layout.healthCheckIntervalSeconds,
		categories: layout.categories.map((category) => mapCategoryToApi(category, layout.id))
	};
}

/** Загружает раскладку с backend (GET /api/layout) и переводит её в модель фронта. */
export async function loadLayout(): Promise<DashboardLayout> {
	const response = await fetch(LAYOUT_ENDPOINT, {
		method: 'GET',
		headers: { Accept: 'application/json' }
	});

	if (!response.ok) {
		throw new Error(`Запрос раскладки завершился с кодом ${response.status}.`);
	}

	const raw = (await response.json()) as ApiDashboardLayout;
	return mapLayoutFromApi(raw);
}

/** Сохраняет раскладку на backend (PUT /api/layout). Успешный ответ — 204 No Content. */
export async function saveLayout(layout: DashboardLayout): Promise<void> {
	const response = await fetch(LAYOUT_ENDPOINT, {
		method: 'PUT',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(mapLayoutToApi(layout))
	});

	if (!response.ok) {
		throw new Error(`Сохранение раскладки завершилось с кодом ${response.status}.`);
	}
}
