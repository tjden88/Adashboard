import { defineConfig } from "vite";

/**
 * Сборка расширения: многостраничная — popup.html и options.html остаются под своими именами,
 * статика из public (manifest.json и иконки) копируется в dist как есть.
 */
export default defineConfig({
    // Относительный base: расширение загружается как локальный пакет, абсолютные пути в нём не работают.
    base: "./",
    build: {
        outDir: "dist",
        emptyOutDir: true,
        // CSP страниц расширения запрещает inline-скрипты, поэтому полифилл modulepreload не внедряем.
        modulePreload: { polyfill: false },
        rollupOptions: {
            input: {
                popup: "popup.html",
                options: "options.html",
            },
        },
    },
});
