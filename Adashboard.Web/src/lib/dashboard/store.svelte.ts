import { checkCardHealth } from './health';
import { resolveDefaultBackgroundColor } from './colors';
import { createSeedLayout } from './seed';
import {
	DEFAULT_CATEGORY_WIDTH,
	MAX_CATEGORY_WIDTH,
	MAX_CONCURRENT_HEALTH_CHECKS,
	MAX_HEALTH_INTERVAL_SECONDS,
	MIN_CATEGORY_WIDTH,
	MIN_HEALTH_INTERVAL_SECONDS,
	type DashboardCard,
	type DashboardCategory,
	type DashboardLayout,
	type HealthState,
	type SiteSettings
} from './types';

/**
 * Состояние dashboard в памяти.
 * Данные не сохраняются: при перезагрузке страницы раскладка снова берётся из сида.
 */
class DashboardStore {
	/** Корневая раскладка. */
	layout = $state<DashboardLayout>(createSeedLayout());

	/** Включён ли режим редактирования. */
	editMode = $state(false);

	/** Состояния доступности карточек по идентификатору. */
	health = $state<Record<number, HealthState>>({});

	/**
	 * Версия раскладки. Увеличивается при структурных изменениях,
	 * чтобы компонент сетки перестроил gridstack.
	 */
	layoutVersion = $state(0);

	/**
	 * Реестр всех карточек по идентификатору, независимый от принадлежности категории.
	 * Нужен при переносе карточек между категориями: карточка на короткое время
	 * не входит ни в одну категорию, и её нельзя потерять.
	 */
	private cardRegistry = new Map<number, DashboardCard>();

	constructor() {
		for (const category of this.layout.categories) {
			for (const card of category.cards) {
				this.cardRegistry.set(card.id, card);
			}
		}
	}

	/** Категории, отсортированные по порядку. */
	get categories(): DashboardCategory[] {
		return [...this.layout.categories].sort((a, b) => a.order - b.order);
	}

	/** Карточки категории, отсортированные по порядку. */
	cardsOf(category: DashboardCategory): DashboardCard[] {
		return [...category.cards].sort((a, b) => a.order - b.order);
	}

	/** Все карточки раскладки. */
	allCards(): DashboardCard[] {
		return this.layout.categories.flatMap((category) => category.cards);
	}

	/** Состояние доступности карточки (по умолчанию — неизвестно). */
	healthOf(cardId: number): HealthState {
		return this.health[cardId] ?? 'unknown';
	}

	/** Переключает режим редактирования. */
	toggleEditMode(): void {
		this.editMode = !this.editMode;
		this.bumpLayout();
	}

	/** Следующий свободный идентификатор категории. */
	nextCategoryId(): number {
		const ids = this.layout.categories.map((category) => category.id);
		return ids.length > 0 ? Math.max(...ids) + 1 : 1;
	}

	/** Следующий свободный идентификатор карточки. */
	nextCardId(): number {
		const ids = this.allCards().map((card) => card.id);
		return ids.length > 0 ? Math.max(...ids) + 1 : 1;
	}

	/** Добавляет категорию в конец списка. */
	addCategory(title: string, width: number): void {
		const order = this.layout.categories.length > 0 ? Math.max(...this.layout.categories.map((c) => c.order)) + 1 : 0;

		this.layout.categories.push({
			id: this.nextCategoryId(),
			title,
			width: clampCategoryWidth(width),
			order,
			cards: []
		});
		this.bumpLayout();
	}

	/** Изменяет название и ширину категории. */
	updateCategory(id: number, title: string, width: number): void {
		const category = this.layout.categories.find((c) => c.id === id);
		if (!category) {
			return;
		}

		category.title = title;
		category.width = clampCategoryWidth(width);
		this.bumpLayout();
	}

	/** Удаляет категорию вместе с её карточками. */
	deleteCategory(id: number): void {
		const category = this.layout.categories.find((c) => c.id === id);
		if (!category) {
			return;
		}

		for (const card of category.cards) {
			this.cardRegistry.delete(card.id);
			delete this.health[card.id];
		}

		this.layout.categories = this.layout.categories.filter((c) => c.id !== id);
		reassignOrders(this.layout.categories);
		this.bumpLayout();
	}

	/** Добавляет карточку в категорию. Идентификатор назначается здесь. */
	addCard(categoryId: number, card: Omit<DashboardCard, 'id' | 'categoryId' | 'order'>): DashboardCard | null {
		const category = this.layout.categories.find((c) => c.id === categoryId);
		if (!category) {
			return null;
		}

		const order = category.cards.length > 0 ? Math.max(...category.cards.map((c) => c.order)) + 1 : 0;
		const created: DashboardCard = { ...card, id: this.nextCardId(), categoryId, order };
		category.cards.push(created);
		this.cardRegistry.set(created.id, created);
		delete this.health[created.id];
		this.bumpLayout();
		return created;
	}

	/** Заменяет поля существующей карточки. */
	updateCard(cardId: number, changes: Omit<DashboardCard, 'id' | 'categoryId' | 'order'>): void {
		const card = this.allCards().find((c) => c.id === cardId);
		if (!card) {
			return;
		}

		Object.assign(card, changes);
		this.bumpLayout();
	}

	/** Удаляет карточку и пересчитывает порядок в её категории. */
	deleteCard(cardId: number): void {
		for (const category of this.layout.categories) {
			const index = category.cards.findIndex((card) => card.id === cardId);
			if (index < 0) {
				continue;
			}

			category.cards.splice(index, 1);
			reassignOrders(category.cards);
			break;
		}

		this.cardRegistry.delete(cardId);
		delete this.health[cardId];
		this.bumpLayout();
	}

	/** Применяет новый порядок категорий (массив идентификаторов в нужном порядке). */
	reorderCategories(orderedIds: number[]): void {
		const position = new Map<number, number>();
		orderedIds.forEach((id, index) => position.set(id, index));

		// Категории, которых нет в присланном списке, уходят в конец.
		let next = orderedIds.length;
		for (const category of this.layout.categories) {
			if (!position.has(category.id)) {
				position.set(category.id, next++);
			}
		}

		for (const category of this.layout.categories) {
			category.order = position.get(category.id) ?? 0;
		}

		this.bumpLayout();
	}

	/**
	 * Переносит карточку в категорию на заданную позицию.
	 * Карточка убирается из всех списков и вставляется в целевой — дубликаты исключены.
	 */
	moveCard(cardId: number, targetCategoryId: number, targetIndex: number): void {
		const card = this.cardRegistry.get(cardId);
		const target = this.layout.categories.find((category) => category.id === targetCategoryId);
		if (!card || !target) {
			return;
		}

		for (const category of this.layout.categories) {
			category.cards = category.cards.filter((item) => item.id !== cardId);
		}

		const index = Math.max(0, Math.min(targetIndex, target.cards.length));
		card.categoryId = target.id;
		target.cards.splice(index, 0, card);

		for (const category of this.layout.categories) {
			reassignOrders(category.cards);
		}

		this.bumpLayout();
	}

	/**
	 * Применяет результат перетаскивания карточек внутри одной категории.
	 * Идентификаторы берутся из события dnd и считаются авторитетными.
	 * Карточки находятся в реестре, поэтому перенос между категориями ничего не теряет.
	 * @param bump пересобирать ли сетку gridstack (нужно только по завершении).
	 */
	applyCardDrop(categoryId: number, orderedIds: number[], bump = true): void {
		const category = this.layout.categories.find((c) => c.id === categoryId);
		if (!category) {
			return;
		}

		// В событии перетаскивания карточка может встретиться дважды: как реальная
		// и как «тень» с тем же id. Убираем дубликаты, сохраняя порядок.
		const seen = new Set<number>();
		const uniqueIds = orderedIds.filter((id) => {
			if (seen.has(id)) {
				return false;
			}

			seen.add(id);
			return true;
		});

		const resolved = uniqueIds
			.map((id) => this.cardRegistry.get(id))
			.filter((card): card is DashboardCard => card !== undefined)
			.map((card, index) => {
				card.categoryId = categoryId;
				card.order = index;
				return card;
			});

		category.cards = resolved;

		// Гарантируем инвариант: каждая карточка ровно в одной категории.
		this.reconcile();

		if (bump) {
			this.bumpLayout();
		}
	}

	/**
	 * Приводит раскладку к согласованному виду: карточка, чей categoryId указывает
	 * на другую категорию, из текущей убирается; карточка, оставшаяся вне всех списков,
	 * возвращается в свою категорию. Защищает от неполных событий перетаскивания.
	 */
	private reconcile(): void {
		for (const item of this.layout.categories) {
			item.cards = item.cards.filter((card) => card.categoryId === item.id);
		}

		const present = new Set(this.allCards().map((card) => card.id));

		for (const [id, card] of this.cardRegistry) {
			if (present.has(id)) {
				continue;
			}

			const target =
				this.layout.categories.find((item) => item.id === card.categoryId) ?? this.layout.categories[0];
			if (target) {
				card.categoryId = target.id;
				target.cards.push(card);
			}
		}

		for (const item of this.layout.categories) {
			reassignOrders(item.cards);
		}
	}

	/** Обновляет настройки сайта (тема, заголовок, интервал проверки и т.д.). */
	updateSettings(settings: SiteSettings): void {
		this.layout.themeMode = settings.themeMode;
		this.layout.siteTitle = settings.siteTitle;
		this.layout.siteDescription = settings.siteDescription;
		this.layout.showLogo = settings.showLogo;
		this.layout.healthCheckIntervalSeconds = Math.min(
			Math.max(settings.healthCheckIntervalSeconds, MIN_HEALTH_INTERVAL_SECONDS),
			MAX_HEALTH_INTERVAL_SECONDS
		);
		this.bumpLayout();
	}

	/** Немедленно проверяет одну карточку. */
	async checkCardNow(card: DashboardCard): Promise<void> {
		if (card.statusMode === 'none') {
			delete this.health[card.id];
			return;
		}

		try {
			this.health[card.id] = await checkCardHealth(card);
		} catch {
			// Отмена или сбой сети: состояние остаётся прежним.
		}
	}

	/** Проверяет все карточки с включённым статусом, ограничивая параллелизм. */
	async checkAllCards(signal: AbortSignal): Promise<void> {
		const cards = this.allCards().filter((card) => card.statusMode !== 'none');
		if (cards.length === 0) {
			return;
		}

		let index = 0;
		const workerCount = Math.min(MAX_CONCURRENT_HEALTH_CHECKS, cards.length);

		const workers = Array.from({ length: workerCount }, async () => {
			while (index < cards.length) {
				const card = cards[index++];
				const state = await checkCardHealth(card, signal);
				this.health[card.id] = state;
			}
		});

		await Promise.all(workers);
	}

	/** Приводит раскладку к корректному виду после правок. */
	normalize(): void {
		this.layout.healthCheckIntervalSeconds = Math.min(
			Math.max(this.layout.healthCheckIntervalSeconds || 30, MIN_HEALTH_INTERVAL_SECONDS),
			MAX_HEALTH_INTERVAL_SECONDS
		);

		if (this.layout.themeMode !== 'dark' && this.layout.themeMode !== 'light') {
			this.layout.themeMode = 'auto';
		}

		for (const category of this.layout.categories) {
			category.width = clampCategoryWidth(category.width);

			for (const card of category.cards) {
				card.categoryId = category.id;

				if (!card.backgroundColor) {
					card.backgroundColor = resolveDefaultBackgroundColor(card.iconClass);
				}
			}

			reassignOrders(category.cards);
		}

		reassignOrders(this.layout.categories);
	}

	private bumpLayout(): void {
		this.layoutVersion++;
	}
}

/** Ограничивает ширину категории допустимым диапазоном. */
function clampCategoryWidth(width: number): number {
	if (!Number.isFinite(width)) {
		return DEFAULT_CATEGORY_WIDTH;
	}

	return Math.min(Math.max(Math.round(width), MIN_CATEGORY_WIDTH), MAX_CATEGORY_WIDTH);
}

/** Пересчитывает порядок элементов по их текущему расположению в массиве. */
function reassignOrders(items: { order: number }[]): void {
	items.forEach((item, index) => {
		item.order = index;
	});
}

/** Единственный экземпляр хранилища. */
export const dashboard = new DashboardStore();
