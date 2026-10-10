<script lang="ts">
	// Сетка категорий на gridstack (12 колонок, ширина категории 1–12).
	// gridstack отвечает только за размещение; перетаскивание — нативное (HTML5 DnD).
	import type { GridStackOptions } from 'gridstack';
	import { onMount } from 'svelte';
	import CategoryPanel from './CategoryPanel.svelte';
	import { dashboard } from '../dashboard/store.svelte';
	import { DRAG_CATEGORY_PREFIX, findInsertIndex, getDragData } from '../dashboard/dnd';
	import type { DashboardCard, DashboardCategory } from '../dashboard/types';

	let { onAddCard, onEditCategory, onDeleteCategory, onEditCard }: {
		onAddCard: (category: DashboardCategory) => void;
		onEditCategory: (category: DashboardCategory) => void;
		onDeleteCategory: (category: DashboardCategory) => void;
		onEditCard: (card: DashboardCard) => void;
	} = $props();

	const categories = $derived(dashboard.categories);

	let gridEl = $state<HTMLDivElement | null>(null);
	let grid: import('gridstack').GridStack | null = null;
	// gridstack подгружается динамически: при SSR его ESM-импорты не резолвятся в Node.
	let GridStackCtor: typeof import('gridstack').GridStack | null = null;

	const gridOptions: GridStackOptions = {
		column: 12,
		// Высота строки — 20px: содержимое измеряется и переводится в строки достаточно точно.
		cellHeight: 20,
		sizeToContent: true,
		// Перемещение и изменение размеров отключены: их делает наш обработчик.
		disableDrag: true,
		disableResize: true,
		margin: 8
	};

	/**
	 * Пересобирает сетку.
	 * Перед инициализацией убираем позиции, расставленные gridstack, чтобы элементы
	 * снова укладывались в порядке их следования в DOM (нужно после перестановки).
	 */
	function rebuildGrid() {
		const el = gridEl;
		if (!el || !GridStackCtor) {
			return;
		}

		grid?.destroy(false);
		grid = null;

		for (const item of el.querySelectorAll<HTMLElement>('.grid-stack-item')) {
			// Ширину передаём через data-атрибут (Svelte не разрешает произвольные gs-*),
			// а перед инициализацией переносим её в атрибут, который читает gridstack.
			const width = item.dataset.gsW;
			if (width) {
				item.setAttribute('gs-w', width);
			}

			for (const attribute of ['gs-x', 'gs-y', 'gs-h', 'gs-min-h', 'gs-max-h']) {
				item.removeAttribute(attribute);
			}

			item.style.left = '';
			item.style.top = '';
			item.style.width = '';
			item.style.height = '';
		}

		const instance = GridStackCtor.init(gridOptions, el);
		if (!instance) {
			return;
		}

		grid = instance;

		// Подгоняем высоту каждого элемента под его содержимое (masonry-укладка).
		for (const item of el.querySelectorAll<HTMLElement>('.grid-stack-item')) {
			instance.resizeToContent(item);
		}
	}

	onMount(() => {
		let disposed = false;

		import('gridstack').then((module) => {
			if (disposed) {
				return;
			}

			GridStackCtor = module.GridStack;
			rebuildGrid();
		});

		return () => {
			disposed = true;
			grid?.destroy(false);
			grid = null;
		};
	});

	// Пересобираем сетку при изменении состава/ширины категорий и режима редактирования.
	$effect(() => {
		// Явные зависимости эффекта.
		void dashboard.layoutVersion;
		void dashboard.editMode;
		const el = gridEl;
		if (el) {
			rebuildGrid();
		}
	});

	function handleDragOver(event: DragEvent) {
		if (!dashboard.editMode) {
			return;
		}

		event.preventDefault();
		if (event.dataTransfer) {
			event.dataTransfer.dropEffect = 'move';
		}
	}

	/** Завершение перетаскивания категории: вычисляем новый порядок и сохраняем его. */
	function handleDrop(event: DragEvent) {
		if (!dashboard.editMode) {
			return;
		}

		const data = getDragData(event);
		if (!data.startsWith(DRAG_CATEGORY_PREFIX)) {
			return;
		}

		event.preventDefault();

		const movedId = Number(data.slice(DRAG_CATEGORY_PREFIX.length));
		const container = gridEl;
		if (!container || !Number.isInteger(movedId)) {
			return;
		}

		const items = [...container.querySelectorAll<HTMLElement>('.grid-stack-item')];
		const others = items.filter((item) => Number(item.dataset.gsId) !== movedId);
		const insertIndex = findInsertIndex(others, event.clientX, event.clientY);

		const order = others.map((item) => Number(item.dataset.gsId));
		order.splice(insertIndex, 0, movedId);

		dashboard.reorderCategories(order);
	}
</script>

<div
	class="grid-stack relative w-full"
	bind:this={gridEl}
	role="region"
	aria-label="Категории dashboard"
	ondragover={handleDragOver}
	ondrop={handleDrop}
>
	{#each categories as category (category.id)}
		<div class="grid-stack-item" data-gs-w={category.width} data-gs-id={category.id}>
			<div class="grid-stack-item-content">
				<CategoryPanel
					{category}
					{onAddCard}
					{onEditCategory}
					{onDeleteCategory}
					{onEditCard}
				/>
			</div>
		</div>
	{/each}
</div>
