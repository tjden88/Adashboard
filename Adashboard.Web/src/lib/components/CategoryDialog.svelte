<script lang="ts">
	// Диалог создания и редактирования категории: название и ширина в колонках.
	import { untrack } from 'svelte';
	import BaseDialog from './BaseDialog.svelte';
	import {
		DEFAULT_CATEGORY_WIDTH,
		MAX_CATEGORY_WIDTH,
		MIN_CATEGORY_WIDTH
	} from '../dashboard/types';

	let { category = null, onsave, onclose }: {
		category?: { title: string; width: number } | null;
		onsave: (title: string, width: number) => void;
		onclose: () => void;
	} = $props();

	// Диалог монтируется заново при каждом открытии, поэтому начальные значения
	// считываем один раз через untrack.
	const initial = untrack(() => ({
		title: category?.title ?? '',
		width: category?.width ?? DEFAULT_CATEGORY_WIDTH
	}));

	let title = $state(initial.title);
	let width = $state(initial.width);

	const isTitleInvalid = $derived(title.trim().length === 0);

	function handleWidth(event: Event) {
		width = Math.min(
			Math.max(Number((event.currentTarget as HTMLInputElement).value), MIN_CATEGORY_WIDTH),
			MAX_CATEGORY_WIDTH
		);
	}

	function save() {
		if (isTitleInvalid) {
			return;
		}

		onsave(title.trim(), width);
	}
</script>

<BaseDialog open={true} title={category ? 'Редактировать категорию' : 'Создать категорию'} onclose={onclose}>
	<form
		class="flex flex-col gap-4"
		onsubmit={(event) => {
			event.preventDefault();
			save();
		}}
	>
		<label class="flex flex-col gap-1.5 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">Название категории</span>
			<input
				class="form-input"
				type="text"
				maxlength="120"
				autocomplete="off"
				placeholder="Обязательно"
				bind:value={title}
			/>
		</label>

		<label class="flex flex-col gap-1.5 text-sm">
			<span class="flex items-center justify-between">
				<span class="font-semibold text-slate-600 dark:text-slate-300">Ширина</span>
				<span class="text-slate-500 dark:text-slate-400">{width} из 12</span>
			</span>
			<input
				type="range"
				min={MIN_CATEGORY_WIDTH}
				max={MAX_CATEGORY_WIDTH}
				step="1"
				value={width}
				oninput={handleWidth}
				class="accent-orange-500"
				aria-valuetext="{width} из 12 колонок"
			/>
		</label>

		<div class="flex justify-end gap-2 pt-1">
			<button type="submit" class="btn btn-primary" disabled={isTitleInvalid}>
				<i class="fa-solid fa-floppy-disk" aria-hidden="true"></i>Сохранить
			</button>
		</div>
	</form>
</BaseDialog>
