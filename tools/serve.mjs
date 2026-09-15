// Gelistirme sunucusu:  npm run serve  → http://localhost:8080
// Kamera ve PWA kurulumu icin guvenli baglam gerekir; localhost guvenli sayilir.

import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';

const ROOT = new URL('../pwa/', import.meta.url).pathname;
const PORT = Number(process.env.PORT ?? 8080);

const TURLER = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8',
  '.svg': 'image/svg+xml',
};

createServer(async (req, res) => {
  const istenen = normalize(decodeURIComponent(new URL(req.url, 'http://x').pathname));
  const yol = join(ROOT, istenen === '/' ? 'index.html' : istenen);

  if (!yol.startsWith(ROOT)) {
    res.writeHead(403).end('Yasak');
    return;
  }

  try {
    const icerik = await readFile(yol);
    res.writeHead(200, {
      'content-type': TURLER[extname(yol)] ?? 'application/octet-stream',
      'cache-control': 'no-store',
    }).end(icerik);
  } catch {
    res.writeHead(404).end('Bulunamadi');
  }
}).listen(PORT, () => console.log(`http://localhost:${PORT}`));
