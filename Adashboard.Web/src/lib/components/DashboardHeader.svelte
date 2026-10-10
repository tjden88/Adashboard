<script lang="ts">
	// Шапка dashboard: логотип, заголовок, описание и кнопки управления.
	import type { DashboardLayout } from '../dashboard/types';

	let { layout, editMode = false, onToggleEdit, onAddCategory, onOpenSettings }: {
		layout: DashboardLayout;
		editMode?: boolean;
		onToggleEdit: () => void;
		onAddCategory: () => void;
		onOpenSettings: () => void;
	} = $props();
</script>

<header class="flex flex-wrap items-start justify-between gap-3">
	<div class="flex min-w-0 items-center gap-3">
		{#if layout.showLogo}
			<img class="h-12 w-12 shrink-0 object-contain" src="/uploads/backgrounds/logo.png" alt="" />
		{/if}
		<div class="min-w-0">
			<h1 class="truncate text-2xl font-bold tracking-wide">{layout.siteTitle}</h1>
			<p class="mt-1 text-sm text-slate-600 dark:text-slate-300">{layout.siteDescription}</p>
		</div>
	</div>

	<div class="flex flex-wrap items-start gap-2">
		{#if editMode}
			<button
				type="button"
				class="btn btn-success btn-icon"
				onclick={onAddCategory}
				title="Добавить категорию"
				aria-label="Добавить категорию"
			>
				<i class="fa-regular fa-layer-plus" aria-hidden="true"></i>
			</button>
			<button
				type="button"
				class="btn btn-icon"
				onclick={onOpenSettings}
				title="Настройки"
				aria-label="Открыть настройки"
			>
				<i class="fa-solid fa-gear" aria-hidden="true"></i>
			</button>
		{/if}

		<button
			type="button"
			class="btn btn-icon {editMode ? 'btn-primary' : ''}"
			onclick={onToggleEdit}
			aria-pressed={editMode}
			aria-label="Переключить режим редактирования"
			title={editMode ? 'Завершить редактирование' : 'Редактировать'}
		>
			<i class="fa-solid {editMode ? 'fa-check' : 'fa-pen-nib'}" aria-hidden="true"></i>
		</button>
	</div>
</header>
