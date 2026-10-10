<script lang="ts">
	// Диалог настроек dashboard: тема, заголовок, описание, логотип, интервал проверки.
	import { untrack } from 'svelte';
	import BaseDialog from './BaseDialog.svelte';
	import {
		MAX_HEALTH_INTERVAL_SECONDS,
		MIN_HEALTH_INTERVAL_SECONDS,
		type SiteSettings,
		type ThemeMode
	} from '../dashboard/types';

	let { settings, onsave, onclose }: {
		settings: SiteSettings;
		onsave: (settings: SiteSettings) => void;
		onclose: () => void;
	} = $props();

	// Диалог монтируется заново при каждом открытии, поэтому начальные значения
	// считываем один раз через untrack.
	const initial = untrack(() => ({
		themeMode: settings.themeMode,
		title: settings.siteTitle,
		description: settings.siteDescription,
		showLogo: settings.showLogo,
		interval: settings.healthCheckIntervalSeconds
	}));

	let themeMode = $state<ThemeMode>(initial.themeMode);
	let title = $state(initial.title);
	let description = $state(initial.description);
	let showLogo = $state(initial.showLogo);
	let interval = $state(initial.interval);

	function clampInterval(value: number): number {
		if (!Number.isFinite(value)) {
			return MIN_HEALTH_INTERVAL_SECONDS;
		}

		return Math.min(Math.max(Math.round(value), MIN_HEALTH_INTERVAL_SECONDS), MAX_HEALTH_INTERVAL_SECONDS);
	}

	function handleInterval(event: Event) {
		interval = clampInterval(Number((event.currentTarget as HTMLInputElement).value));
	}

	function save() {
		onsave({
			themeMode,
			siteTitle: title.trim(),
			siteDescription: description.trim(),
			showLogo,
			healthCheckIntervalSeconds: clampInterval(interval)
		});
	}
</script>

<BaseDialog open={true} title="Настройки" onclose={onclose}>
	<form
		class="flex flex-col gap-4"
		onsubmit={(event) => {
			event.preventDefault();
			save();
		}}
	>
		<label class="flex flex-col gap-1.5 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">Заголовок dashboard</span>
			<input class="form-input" type="text" maxlength="120" autocomplete="off" bind:value={title} />
		</label>

		<label class="flex flex-col gap-1.5 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">Описание</span>
			<input class="form-input" type="text" maxlength="300" autocomplete="off" bind:value={description} />
		</label>

		<label class="flex items-center gap-2 text-sm">
			<input
				class="h-4 w-4 accent-orange-500"
				type="checkbox"
				bind:checked={showLogo}
			/>
			<span>Показывать логотип в шапке</span>
		</label>

		<label class="flex flex-col gap-1.5 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">Тема оформления</span>
			<select class="form-input" bind:value={themeMode}>
				<option value="auto">Автоматическая</option>
				<option value="dark">Тёмная</option>
				<option value="light">Светлая</option>
			</select>
		</label>

		<label class="flex flex-col gap-1.5 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">Интервал проверки статуса, сек</span>
			<input
				class="form-input"
				type="number"
				min={MIN_HEALTH_INTERVAL_SECONDS}
				max={MAX_HEALTH_INTERVAL_SECONDS}
				step="5"
				value={interval}
				oninput={handleInterval}
			/>
			<span class="text-xs text-slate-500 dark:text-slate-400">
				Как часто проверять доступность сервисов с включённым статусом (10–600 секунд).
			</span>
		</label>

		<div class="flex justify-end gap-2 pt-1">
			<button type="submit" class="btn btn-primary">
				<i class="fa-solid fa-floppy-disk" aria-hidden="true"></i>Сохранить
			</button>
		</div>
	</form>
</BaseDialog>
