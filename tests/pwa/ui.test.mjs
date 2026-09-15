// Tarayici ucu uca testi: uygulama gercekten acilip dogru sifreyi uretiyor mu?
//   node --test tests/pwa/ui.test.mjs
//
// Kamera basit bir test ortaminda taklit edilemedigi icin eslestirme dogrudan
// kasaya yazilir; sonrasindaki akis (PIN, tahta secimi, elle kod, sifre) gercek
// arayuz uzerinden surulur. Beklenen sifre spec/vectors.json'dan gelir.

import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';
import { existsSync, readdirSync } from 'node:fs';
import { chromium } from 'playwright';

/** Hazir kurulu Chromium'u bulur; yoksa Playwright'in kendi indirdigini kullanir. */
function chromiumYolu() {
  const kok = process.env.PLAYWRIGHT_BROWSERS_PATH;
  if (!kok || !existsSync(kok)) return undefined;

  const klasor = readdirSync(kok).find((a) => a.startsWith('chromium-'));
  if (!klasor) return undefined;

  const yol = join(kok, klasor, 'chrome-linux', 'chrome');
  return existsSync(yol) ? yol : undefined;
}

const ROOT = new URL('../../pwa/', import.meta.url).pathname;
const vectors = JSON.parse(readFileSync(new URL('../../spec/vectors.json', import.meta.url), 'utf8'));
const ORNEK = vectors.cases[0];  // { kod, cevap }

const TURLER = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.webmanifest': 'application/manifest+json',
  '.svg': 'image/svg+xml',
};

let server, browser, page, baseUrl;

before(async () => {
  server = createServer(async (req, res) => {
    const istenen = normalize(new URL(req.url, 'http://x').pathname);
    const yol = join(ROOT, istenen === '/' ? 'index.html' : istenen);
    try {
      res.writeHead(200, { 'content-type': TURLER[extname(yol)] ?? 'text/plain' })
        .end(await readFile(yol));
    } catch {
      res.writeHead(404).end();
    }
  });

  await new Promise((r) => server.listen(0, r));
  baseUrl = `http://localhost:${server.address().port}`;

  browser = await chromium.launch({ executablePath: chromiumYolu() });
  page = await browser.newPage({ viewport: { width: 390, height: 844 } });

  const hatalar = [];
  page.on('pageerror', (e) => hatalar.push(e.message));
  page.hatalar = hatalar;

  await page.goto(`${baseUrl}/index.html`);
});

after(async () => {
  await browser?.close();
  server?.close();
});

test('acilista ana ekran gelir', async () => {
  await page.waitForSelector('#view-home:not([hidden])');
  assert.equal(await page.isVisible('#ac-btn'), true);
});

test('elle girilen kod dogru sifreyi uretir', async () => {
  await page.click('#elle-btn');
  await page.waitForSelector('#view-manual:not([hidden])');

  await page.fill('#manual-kod', ORNEK.kod);
  await page.click('#manual-form button[type=submit]');

  await page.waitForSelector('#view-result:not([hidden])');

  const gosterilen = (await page.textContent('#sonuc-kod')).replace(/\s/g, '');
  assert.equal(gosterilen, ORNEK.cevap, 'Arayuzun urettigi sifre vektorle ayni olmali');
});

test('kod yazarken dortlu gruplara ayriliyor', async () => {
  await page.click('#sonuc-bitti');
  await page.click('#elle-btn');

  await page.fill('#manual-kod', '123456654321');

  assert.equal(await page.inputValue('#manual-kod'), '1234 5665 4321');
});

test('bosluklu yazilan kod da kabul edilir', async () => {
  await page.click('#manual-form button[type=submit]');
  await page.waitForSelector('#view-result:not([hidden])');

  assert.equal((await page.textContent('#sonuc-kod')).replace(/\s/g, ''), '0530865');
});

test('eksik kod reddedilir', async () => {
  await page.click('#sonuc-bitti');
  await page.click('#elle-btn');

  await page.fill('#manual-kod', '12345');
  await page.click('#manual-form button[type=submit]');

  assert.equal(await page.isVisible('#view-result'), false);
  assert.match(await page.textContent('#manual-hata'), /12 haneli/);
});

test('bitti ana ekrana doner', async () => {
  await page.click('[data-geri]');

  await page.waitForSelector('#view-home:not([hidden])');
});

test('sayfada javascript hatasi olusmadi', () => {
  assert.deepEqual(page.hatalar, []);
});
