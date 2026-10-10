<script lang="ts">
	// Поле выбора цвета фона карточки.
	// Использует готовый компонент svelte-awesome-color-picker и ведёт список образцов,
	// в котором недавно выбранные цвета поднимаются наверх.
	import ColorPicker from 'svelte-awesome-color-picker';

	let { value = $bindable('#1e40af'), label = 'Цвет фона' } = $props();

	// Базовый набор образцов (как в старом Blazor-проекте).
	const baseSwatches = [
		'#1e40af',
		'#0369a1',
		'#0ea5e9',
		'#15803d',
		'#22c55e',
		'#ea580c',
		'#f97316',
		'#b91c1c',
		'#f43f5e',
		'#8b5cf6',
		'#0f172a',
		'#64748b'
	];

	let swatches = $state<string[]>([...baseSwatches]);

	/** Поднимает выбранный цвет на первое место и ограничивает список 12 образцами. */
	function remember(hex: string | null | undefined) {
		if (!hex) {
			return;
		}

		const normalized = hex.toLowerCase();
		swatches = [normalized, ...swatches.filter((item) => item.toLowerCase() !== normalized)].slice(0, 12);
	}

	function handleInput(picked: { hex: string | null }) {
		if (picked.hex) {
			value = picked.hex;
			remember(picked.hex);
		}
	}
</script>

<ColorPicker hex={value} isAlpha={false} swatches={swatches} label={label} onInput={handleInput} />
