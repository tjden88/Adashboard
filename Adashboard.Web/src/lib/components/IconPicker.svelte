<script lang="ts">
	// Выбор встроенной иконки Font Awesome с поиском по названию.
	// Значение — CSS-класс иконки (например, "fa-solid fa-heart-pulse").
	import { fontAwesomeIcons } from '../dashboard/icons';

	let { value = $bindable(''), ariaLabel = 'Поиск иконки' } = $props();

	// Текст поиска и признак открытого списка подсказок.
	let query = $state('');
	let open = $state(false);

	// $derived пересчитывает подсказки при изменении query.
	const suggestions = $derived.by(() => {
		const text = query.trim().toLowerCase();
		if (text.length === 0) {
			return [];
		}

		return fontAwesomeIcons.filter((icon) => icon.toLowerCase().includes(text)).slice(0, 30);
	});

	function handleInput(event: Event) {
		query = (event.currentTarget as HTMLInputElement).value;
		open = true;
	}

	function handleKeydown(event: KeyboardEvent) {
		if (event.key === 'Escape') {
			open = false;
		}
	}

	function select(icon: string) {
		value = icon;
		query = icon;
		open = false;
	}
</script>

<div class="relative">
	<div class="flex items-stretch gap-2">
		<span
			class="grid w-11 shrink-0 place-items-center rounded-xl rounded-r-none border border-r-0 border-slate-300/70 bg-white/70 text-orange-500 dark:border-white/10 dark:bg-slate-900/60"
			aria-hidden="true"
		>
			<i class={value || 'fa-solid fa-link'}></i>
		</span>
		<input
			class="form-input rounded-l-none"
			type="text"
			autocomplete="off"
			placeholder="Например: house, github..."
			value={query}
			oninput={handleInput}
			onkeydown={handleKeydown}
			onfocus={() => (open = true)}
			aria-label={ariaLabel}
		/>
		<a
			class="grid w-11 shrink-0 place-items-center rounded-xl border border-slate-300/70 bg-white/70 text-orange-500 dark:border-white/10 dark:bg-slate-900/60"
			href="https://fontawesome.com/search"
			target="_blank"
			rel="noopener noreferrer"
			title="Поиск иконок на fontawesome.com"
			aria-label="Открыть поиск иконок Font Awesome в новой вкладке"
		>
			<i class="fa-solid fa-arrow-up-right-from-square" aria-hidden="true"></i>
		</a>
	</div>

	{#if open && suggestions.length > 0}
		<ul
			class="absolute top-full right-0 left-0 z-20 mt-1 max-h-52 overflow-y-auto rounded-xl border border-slate-300/70 bg-white p-1 shadow-xl dark:border-white/10 dark:bg-slate-900"
			role="listbox"
		>
			{#each suggestions as icon (icon)}
				<li role="option" aria-selected={value === icon}>
					<button
						type="button"
						class="flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-2 py-1.5 text-left text-sm hover:bg-orange-100/60 dark:hover:bg-orange-500/20"
						onclick={() => select(icon)}
					>
						<i class="{icon} w-4 text-center text-orange-500" aria-hidden="true"></i>
						<span>{icon}</span>
					</button>
				</li>
			{/each}
		</ul>
	{/if}
</div>
