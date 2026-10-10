<script lang="ts">
	// Карточка ссылки. Открывает сервис в новой вкладке.
	// В режиме редактирования клик открывает диалог, а не ссылку.
	import { resolveCardIconColor, resolveCardTextColor } from '../dashboard/colors';
	import { DRAG_CARD_PREFIX, setDragData } from '../dashboard/dnd';
	import type { DashboardCard, HealthState } from '../dashboard/types';

	let { card, health = 'unknown', editMode = false, colsDesktop = 1, colsMedium = 1, colsMobile = 1, onedit }: {
		card: DashboardCard;
		health?: HealthState;
		editMode?: boolean;
		colsDesktop?: number;
		colsMedium?: number;
		colsMobile?: number;
		onedit?: (card: DashboardCard) => void;
	} = $props();

	/** Широкая карточка занимает две колонки, но не больше доступных. */
	function span(isWide: boolean, columns: number): number {
		return isWide ? Math.min(2, Math.max(1, columns)) : 1;
	}

	const spanDesktop = $derived(span(card.isWide, colsDesktop));
	const spanMedium = $derived(span(card.isWide, colsMedium));
	const spanMobile = $derived(span(card.isWide, colsMobile));

	const iconColor = $derived(resolveCardIconColor(card.backgroundColor));
	const textColor = $derived(resolveCardTextColor(card.backgroundColor));

	const statusClass = $derived(
		health === 'online' ? 'bg-green-500' : health === 'offline' ? 'bg-red-500' : 'bg-slate-400'
	);
	const statusLabel = $derived(
		health === 'online' ? 'Сервис доступен' : health === 'offline' ? 'Сервис недоступен' : 'Статус проверяется'
	);

	function handleClick(event: MouseEvent) {
		if (editMode) {
			event.preventDefault();
			onedit?.(card);
		}
	}

	/** Начало перетаскивания карточки (доступно только в режиме редактирования). */
	function handleDragStart(event: DragEvent) {
		if (!editMode) {
			event.preventDefault();
			return;
		}

		setDragData(event, `${DRAG_CARD_PREFIX}${card.id}`);
	}
</script>

<a
	class="card-link relative flex flex-col items-center justify-center overflow-hidden rounded-xl p-1.5 no-underline shadow-sm transition hover:shadow-lg focus-visible:ring-2 focus-visible:ring-offset-1 focus-visible:outline-none"
	href={card.urlScheme + card.url}
	target="_blank"
	rel="noopener noreferrer"
	data-card-id={card.id}
	draggable={editMode}
	style="--card-span:{spanDesktop}; --card-span-medium:{spanMedium}; --card-span-mobile:{spanMobile}; background-color:{card.backgroundColor}; color:{textColor};"
	onclick={handleClick}
	ondragstart={handleDragStart}
>
	<span class="w-full truncate text-center text-xs font-semibold" style="color:{textColor}">{card.title}</span>

	<div class="relative grid min-h-0 w-full flex-1 place-items-center" style="color:{iconColor}">
		<i class="{card.iconClass} text-[2.4rem] leading-none" aria-hidden="true"></i>
		{#if card.imagePath}
			<img
				class="absolute inset-0 m-auto max-h-[72%] max-w-[72%] object-contain"
				src={card.imagePath}
				alt=""
				loading="lazy"
				onerror={(event) => ((event.currentTarget as HTMLImageElement).hidden = true)}
			/>
		{/if}
	</div>

	{#if card.statusMode !== 'none'}
		<span
			class="status-dot absolute right-2 bottom-2 h-2 w-2 rounded-full {statusClass}"
			role="img"
			aria-label={statusLabel}
			title={statusLabel}
		></span>
	{/if}
</a>
