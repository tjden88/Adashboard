// Вспомогательные функции перетаскивания на нативном HTML5 Drag and Drop.
// Используются, потому что svelte-dnd-action нестабильно работает с абсолютной
// раскладкой gridstack: теряет и дублирует перетаскиваемые элементы.

/** Префиксы полезной нагрузки, которая передаётся через dataTransfer. */
export const DRAG_CARD_PREFIX = 'card:';
export const DRAG_CATEGORY_PREFIX = 'category:';

/** Записывает в dataTransfer идентификатор перетаскиваемого элемента. */
export function setDragData(event: DragEvent, value: string): void {
	if (!event.dataTransfer) {
		return;
	}

	event.dataTransfer.setData('text/plain', value);
	event.dataTransfer.effectAllowed = 'move';
}

/** Читает из dataTransfer строку, записанную при начале перетаскивания. */
export function getDragData(event: DragEvent): string {
	return event.dataTransfer?.getData('text/plain') ?? '';
}

/**
 * Определяет индекс вставки по координатам курсора.
 * Элементы перебираются в порядке чтения: сначала по строкам, затем слева направо.
 */
export function findInsertIndex(elements: HTMLElement[], x: number, y: number): number {
	for (let index = 0; index < elements.length; index++) {
		const rect = elements[index].getBoundingClientRect();

		if (y < rect.top) {
			return index;
		}

		if (y <= rect.bottom && x < rect.left + rect.width / 2) {
			return index;
		}
	}

	return elements.length;
}
