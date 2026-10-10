import adapter from '@sveltejs/adapter-node';
import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [
		// Tailwind CSS v4 подключается как плагин Vite, отдельный tailwind.config не нужен.
		tailwindcss(),
		sveltekit({
			compilerOptions: {
				// Принудительно включает режим рун для проекта, кроме библиотек. Можно убрать в Svelte 6.
				runes: ({ filename }) =>
					filename.split(/[/\\]/).includes('node_modules') ? undefined : true
			},
			adapter: adapter()
		})
	],
	// Дев-прокси: запросы к /api уходят на backend Adashboard.Api.
	// Благодаря этому браузер обращается к тому же origin, что и фронт, и CORS настраивать не нужно.
	server: {
		proxy: {
			'/api': {
				target: 'http://localhost:5024',
				changeOrigin: true
			}
		}
	}
});
