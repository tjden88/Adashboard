/**
 * Кроссплатформенный доступ к API браузера: в Firefox глобальный объект browser,
 * в Chrome и Edge — chrome. Промисы поддерживаются обеими сторонами, полифилл не нужен.
 */
export const browserApi = globalThis.browser ?? globalThis.chrome;
