// Service worker опубликованного приложения.
//
// После первой загрузки он складывает все статические файлы приложения в отдельный
// кэш и при последующих открытиях отдаёт их из кэша без обращения к сети. Именно это
// убирает десятки сетевых «кругов» (round-trip) и делает запуск быстрым даже при
// медленном соединении. Файл используется только после публикации; в режиме
// разработки работает wwwroot/service-worker.js.
//
// Список файлов и их хеши сборка кладёт в генерируемый service-worker-assets.js.
// О подводных камнях офлайн-режима: https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');

self.addEventListener('install', (event) => event.waitUntil(onInstall(event)));
self.addEventListener('activate', (event) => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', (event) => event.respondWith(onFetch(event)));

// Имя кэша зависит от версии набора ресурсов: при новой публикации создаётся новый
// кэш, а старый удаляется в onActivate.
const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;

// Что кэшируем. Расширения покрывают статику проекта и файлы Blazor WebAssembly.
const offlineAssetsInclude = [
    /\.wasm$/,
    /\.html$/,
    /\.js$/,
    /\.json$/,
    /\.css$/,
    /\.woff2?$/,
    /\.ttf$/,
    /\.png$/,
    /\.jpe?g$/,
    /\.gif$/,
    /\.ico$/,
    /\.svg$/,
    /\.webmanifest$/,
    /\.dat$/,
    /\.blat$/,
];

// Сам манифест ресурсов кэшировать не нужно — он нужен только при установке.
const offlineAssetsExclude = [/^service-worker(-assets)?\.js$/];

async function onInstall(event) {
    console.info('Service worker: установка');

    const cache = await caches.open(cacheName);
    const assets = self.assetsManifest.assets
        .filter((asset) => offlineAssetsInclude.some((pattern) => pattern.test(asset.url)))
        .filter((asset) => !offlineAssetsExclude.some((pattern) => pattern.test(asset.url)));

    // Кэшируем каждый файл отдельно и не валим установку из-за одного проблемного
    // ресурса: такой файл просто будет догружен из сети при первом обращении.
    await Promise.all(assets.map(async (asset) => {
        try {
            await cache.add(new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
        } catch (error) {
            console.warn('Service worker: не удалось закэшировать', asset.url, error);
        }
    }));
}

async function onActivate(event) {
    console.info('Service worker: активация');

    // Удаляем кэши предыдущих версий приложения.
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter((key) => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map((key) => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;

    if (event.request.method === 'GET') {
        // Навигационные запросы (открытие и обновление страницы) отдаём сохранённым
        // index.html, поэтому приложение стартует при любом маршруте.
        const shouldServeIndexHtml = event.request.mode === 'navigate';
        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    // Приоритет у кэша; если файла там нет — обращаемся к сети (например, к API).
    return cachedResponse || fetch(event.request);
}
