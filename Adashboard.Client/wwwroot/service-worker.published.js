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

// Сам манифест ресурсов кэшировать не нужно — он нужен только при установке. Версия для
// публикации (service-worker.published.js) в образ не попадает, её тоже пропускаем.
const offlineAssetsExclude = [/^service-worker.*\.js$/];

async function onInstall(event) {
    console.info('Service worker: установка');

    // Новая версия включается сразу, не дожидаясь закрытия всех вкладок приложения:
    // иначе обновление висело бы в ожидании, пока пользователь закроет dashboard.
    self.skipWaiting();

    await cacheAssets();
}

async function onActivate(event) {
    console.info('Service worker: активация');

    // Удаляем кэши предыдущих версий приложения.
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter((key) => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map((key) => caches.delete(key)));

    // Забираем управление уже открытыми вкладками: при skipWaiting новая версия
    // должна обслуживать их сразу, не дожидаясь следующей навигации.
    await self.clients.claim();

    // Дозаполняем недостающие файлы: браузер мог вытеснить кэш под давлением квоты,
    // и без этого шага приложение осталось бы без кэша до следующей публикации.
    await cacheAssets();
}

async function cacheAssets() {
    const cache = await caches.open(cacheName);
    const assets = self.assetsManifest.assets
        .filter((asset) => offlineAssetsInclude.some((pattern) => pattern.test(asset.url)))
        .filter((asset) => !offlineAssetsExclude.some((pattern) => pattern.test(asset.url)));

    // Кэшируем каждый файл отдельно и не валим установку из-за одного проблемного
    // ресурса: такой файл просто будет догружен из сети при первом обращении.
    await Promise.all(assets.map(async (asset) => {
        try {
            if (await cache.match(asset.url)) {
                return;
            }

            await cache.add(new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
        } catch (error) {
            console.warn('Service worker: не удалось закэшировать', asset.url, error);
        }
    }));
}

// Можно ли класть ответ в офлайн-кэш. API отдаёт данные, а не статику, поэтому его
// не кэшируем; скрипт и манифест service worker браузер хранит отдельно.
function isCacheable(request, response) {
    if (!response.ok || response.type !== 'basic' || request.cache === 'no-store') {
        return false;
    }

    const path = new URL(request.url).pathname;
    if (path.startsWith('/api/')) {
        return false;
    }

    return !/\/service-worker.*\.js$/.test(path);
}

async function onFetch(event) {
    if (event.request.method !== 'GET') {
        return fetch(event.request);
    }

    // Навигационные запросы (открытие и обновление страницы) отдаём сохранённым
    // index.html, поэтому приложение стартует при любом маршруте.
    const isNavigation = event.request.mode === 'navigate';
    const cacheKey = isNavigation ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(cacheKey);
    if (cachedResponse) {
        return cachedResponse;
    }

    const response = await fetch(event.request);

    // Промах кэша дозаписываем сразу: если браузер вытеснил данные или установка
    // что-то пропустила, кэш восстановится сам, без ожидания следующей публикации.
    if (isCacheable(event.request, response)) {
        try {
            await cache.put(cacheKey, response.clone());
        } catch (error) {
            console.warn('Service worker: не удалось обновить кэш', event.request.url, error);
        }
    }

    return response;
}
