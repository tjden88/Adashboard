<script lang="ts">
	// Глобальные стили приложения: Tailwind и базовые правила из app.css.
	import '../app.css';
	// Стили gridstack нужны для абсолютного позиционирования категорий в сетке.
	import 'gridstack/dist/gridstack.min.css';
	import type { LayoutProps } from './$types';

	let { children }: LayoutProps = $props();
</script>

{@render children()}
