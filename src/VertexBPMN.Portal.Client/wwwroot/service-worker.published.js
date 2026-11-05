// VertexBPMN Fachportal - Service Worker (published build, offline app shell).
//
// Scope / security notes:
//  - ONLY the static app shell (fingerprinted UI assets + navigating to index.html)
//    is cached. This enables the installable portal to load its shell offline (LC-AC-18).
//  - Authenticated responses, tokens and sensitive business data are NEVER cached here;
//    non-shell requests always go to the network. Offline *task* data is the concern of
//    the explicit per-app offline task feature, not this worker.
//
// self.assetsManifest is injected at publish time by the Blazor toolchain from the
// fingerprinted asset list (see ServiceWorkerAssetsManifest in the .csproj).

const CACHE_NAME = 'vertexbpmn-shell-' + (self.assetsManifest ? self.assetsManifest.version : 'dev');

// All fingerprinted shell assets (current version) are pre-cached on install.
self.addEventListener('install', (event) => {
    event.waitUntil((async () => {
        self.skipWaiting();
        const assets = (self.assetsManifest && self.assetsManifest.assets) || [];
        const cache = await caches.open(CACHE_NAME);
        await Promise.all(assets.map((a) => cache.add(a.url)));
    })());
});

// On activation, drop any cache from a previous version so stale assets are never served.
self.addEventListener('activate', (event) => {
    event.waitUntil((async () => {
        const names = await caches.keys();
        await Promise.all(names.filter((n) => n !== CACHE_NAME).map((n) => caches.delete(n)));
        await self.clients.claim();
    })());
});

self.addEventListener('fetch', (event) => {
    const { request } = event;
    const url = new URL(request.url);

    // Only same-origin, GET, app-shell traffic is handled.
    if (request.method !== 'GET' || url.origin !== self.location.origin) {
        return;
    }

    // 1) Known fingerprinted shell asset -> cache first, network fallback.
    if (self.assetsManifest && self.assetsManifest.assets.some((a) => a.url === url.pathname)) {
        event.respondWith((async () => {
            const cached = await caches.match(request);
            if (cached) {
                return cached;
            }
            try {
                const response = await fetch(request);
                const cache = await caches.open(CACHE_NAME);
                cache.put(request, response.clone());
                return response;
            } catch {
                return await caches.match(request);
            }
        })());
        return;
    }

    // 2) Navigation -> app shell from cache when offline, network first otherwise.
    if (request.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                return await fetch(request);
            } catch {
                return (await caches.match('/index.html')) || Response.error();
            }
        })());
        return;
    }

    // 3) Everything else (API, BFF, auth) -> network only.
});
