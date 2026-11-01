// VertexBPMN Fachportal - Service Worker (development build).
// During development the app shell changes on every rebuild, so this worker does
// NOT precache anything. The published build (service-worker.published.js) is what
// provides the offline app shell; it is emitted in place of this file at publish.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => self.clients.claim());
self.addEventListener('fetch', () => {
    // Network-only during development. No sensitive or API data is cached.
});
