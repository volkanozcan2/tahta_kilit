// Cevrimdisi calisma: tum uygulama dosyalari kurulumda onbellege alinir.
// Surum degistiginde (CACHE) eski onbellek silinir.

const CACHE = 'tahta-kilit-v1';

const DOSYALAR = [
  './',
  './index.html',
  './styles.css',
  './app.js',
  './core.js',
  './store.js',
  './vendor/jsQR.js',
  './manifest.webmanifest',
  './icon.svg',
  './icon-maskable.svg',
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE).then((cache) => cache.addAll(DOSYALAR)).then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((adlar) => Promise.all(adlar.filter((a) => a !== CACHE).map((a) => caches.delete(a))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') return;

  // Onbellek once: uygulama internet olmadan da acilmali.
  event.respondWith(
    caches.match(event.request).then((cached) => cached ?? fetch(event.request)),
  );
});
