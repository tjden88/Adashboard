<script lang="ts">
	// Панель одной категории: заголовок с действиями и сетка карточек.
	// Карточки можно перетаскивать внутри категории и между категориями
	// (нативный HTML5 Drag and Drop). Категория перетаскивается за ручку.
	import LinkCard from './LinkCard.svelte';
	import { dashboard } from '../dashboard/store.svelte';
	import { DRAG_CARD_PREFIX, DRAG_CATEGORY_PREFIX, findInsertIndex, getDragData, setDragData } from '../dashboard/dnd';
	import type { DashboardCard, DashboardCategory } from '../dashboard/types';

	let { category, onAddCard, onEditCategory, onDeleteCategory, onEditCard }: {
		category: DashboardCategory;
		onAddCard: (category: DashboardCategory) => void;
		onEditCategory: (category: DashboardCategory) => void;
		onDeleteCategory: (category: DashboardCategory) => void;
		onEditCard: (card: DashboardCard) => void;
	} = $props();

	const cards = $derived(dashboard.cardsOf(category));

	let cardsEl = $state<HTMLDivElement | null>(null);
	let sectionEl = $state<HTMLElement | null>(null);

	// Число колонок карточек повторяет поведение старого приложения.
	const colsDesktop = $derived(category.width);
	const colsMedium = $derived(Math.min(Math.max(Math.round(category.width / 2), 1), 6));
	const colsMobile = $derived(Math.min(category.width, 4));

	/** Начало перетаскивания категории за ручку. */
	function handleCategoryDragStart(event: DragEvent) {
		if (!dashboard.editMode) {
			return;
		}

		setDragData(event, `${DRAG_CATEGORY_PREFIX}${category.id}`);

		// Тянем образ всей категории, а не маленькой ручки.
		if (event.dataTransfer && sectionEl) {
			event.dataTransfer.setDragImage(sectionEl, 24, 24);
		}
	}

	function handleDragOver(event: DragEvent) {
		if (!dashboard.editMode) {
			return;
		}

		// Предотвращаем действие по умолчанию, чтобы разрешить drop.
		event.preventDefault();
		if (event.dataTransfer) {
			event.dataTransfer.dropEffect = 'move';
		}
	}

	/** Завершение перетаскивания карточки в эту категорию. */
	function handleDrop(event: DragEvent) {
		if (!dashboard.editMode) {
			return;
		}

		const data = getDragData(event);
		if (!data.startsWith(DRAG_CARD_PREFIX)) {
			return;
		}

		event.preventDefault();

		const cardId = Number(data.slice(DRAG_CARD_PREFIX.length));
		if (!Number.isInteger(cardId)) {
			return;
		}

		// Индекс считаем по карточкам, кроме перетаскиваемой (она ещё в исходной позиции).
		const elements = cardsEl
			? [...cardsEl.querySelectorAll<HTMLAnchorElement>('a.card-link')].filter(
					(element) => Number(element.dataset.cardId) !== cardId
				)
			: [];

		const index = findInsertIndex(elements, event.clientX, event.clientY);
		dashboard.moveCard(cardId, category.id, index);
	}
</script>

<section
	bind:this={sectionEl}
	class="flex flex-col gap-3 overflow-hidden rounded-2xl border border-white/50 bg-white/40 p-4 shadow-lg backdrop-blur-sm dark:border-white/10 dark:bg-slate-900/40"
	data-category-id={category.id}
	aria-label="Категория {category.title}"
	ondragover={handleDragOver}
	ondrop={handleDrop}
>
	<header class="flex min-w-0 items-center justify-between gap-2">
		<div class="flex min-w-0 items-center gap-2">
			{#if dashboard.editMode}
				<span
					class="grid h-8 w-8 shrink-0 cursor-grab touch-none place-items-center rounded-lg bg-orange-100 text-orange-600 active:cursor-grabbing dark:bg-orange-500/20 dark:text-orange-300"
					draggable="true"
					ondragstart={handleCategoryDragStart}
					role="button"
					tabindex="0"
					title="Перетащить категорию"
					aria-label="Перетащить категорию {category.title}"
				>
					<i class="fa-solid fa-grip-vertical" aria-hidden="true"></i>
				</span>
			{/if}
			<h2 class="truncate text-base font-bold" title={category.title}>{category.title}</h2>
		</div>

		{#if dashboard.editMode}
			<div class="flex shrink-0 flex-wrap justify-end gap-1">
				<button
					type="button"
					class="btn btn-success btn-icon"
					onclick={() => onAddCard(category)}
					title="Добавить ссылку"
					aria-label="Добавить ссылку в категорию {category.title}"
				>
					<i class="fa-solid fa-plus" aria-hidden="true"></i>
				</button>
				<button
					type="button"
					class="btn btn-primary btn-icon"
					onclick={() => onEditCategory(category)}
					title="Редактировать категорию"
					aria-label="Редактировать категорию {category.title}"
				>
					<i class="fa-solid fa-pen-nib" aria-hidden="true"></i>
				</button>
				<button
					type="button"
					class="btn btn-danger btn-icon"
					onclick={() => onDeleteCategory(category)}
					title="Удалить категорию"
					aria-label="Удалить категорию {category.title}"
				>
					<i class="fa-solid fa-trash" aria-hidden="true"></i>
				</button>
			</div>
		{/if}
	</header>

	<div
		bind:this={cardsEl}
		class="category-cards"
		style="--cards-cols:{colsDesktop}; --cards-cols-medium:{colsMedium}; --cards-cols-mobile:{colsMobile};"
	>
		{#each cards as card (card.id)}
			<LinkCard
				{card}
				health={dashboard.healthOf(card.id)}
				editMode={dashboard.editMode}
				{colsDesktop}
				{colsMedium}
				{colsMobile}
				onedit={onEditCard}
			/>
		{/each}
	</div>
</section>
