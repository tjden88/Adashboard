<script lang="ts">
	// Базовое модальное окно на нативном элементе <dialog>.
	// showModal()/close() дают бесплатные фокус-ловушку, затемнение и закрытие по Escape.
	import type { Snippet } from 'svelte';

	let {
		open = false,
		title = '',
		closeOnOverlay = false,
		onclose,
		children
	}: {
		open?: boolean;
		title?: string;
		closeOnOverlay?: boolean;
		onclose?: () => void;
		children?: Snippet;
	} = $props();

	let dialogEl = $state<HTMLDialogElement | null>(null);

	// Реагируем на изменение пропа open: открываем или закрываем нативный диалог.
	$effect(() => {
		const el = dialogEl;
		if (!el) {
			return;
		}

		if (open && !el.open) {
			el.showModal();
		} else if (!open && el.open) {
			el.close();
		}
	});

	// Escape браузер обрабатывает сам: отдаём закрытие родителю.
	function handleCancel(event: Event) {
		event.preventDefault();
		onclose?.();
	}

	// Клик по затемнённой области приходит с target, равным самому <dialog>.
	function handleClick(event: MouseEvent) {
		if (closeOnOverlay && event.target === dialogEl) {
			onclose?.();
		}
	}
</script>

<dialog
	bind:this={dialogEl}
	class="app-dialog"
	aria-modal="true"
	oncancel={handleCancel}
	onclick={handleClick}
>
	<div
		class="w-[min(520px,92vw)] max-h-[85vh] overflow-y-auto rounded-2xl border border-slate-300/70 bg-white text-slate-800 shadow-2xl dark:border-white/10 dark:bg-slate-900 dark:text-slate-100"
	>
		<header
			class="flex items-center justify-between gap-4 border-b border-slate-200/70 px-5 py-4 dark:border-white/10"
		>
			<h2 class="text-lg font-semibold">{title}</h2>
			<button
				type="button"
				class="cursor-pointer rounded-lg px-2 text-xl leading-none text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white"
				aria-label="Закрыть"
				onclick={() => onclose?.()}
			>
				<i class="fa-solid fa-xmark" aria-hidden="true"></i>
			</button>
		</header>
		<div class="px-5 py-4">
			{@render children?.()}
		</div>
	</div>
</dialog>

<style>
	/* Нативный dialog по умолчанию имеет рамку и фон — убираем их, панель рисуем сами. */
	.app-dialog {
		margin: auto;
		border: none;
		padding: 0;
		background: transparent;
		color: inherit;
		max-width: none;
		max-height: none;
	}

	.app-dialog::backdrop {
		background: rgba(15, 23, 42, 0.55);
		backdrop-filter: blur(4px);
	}
</style>
