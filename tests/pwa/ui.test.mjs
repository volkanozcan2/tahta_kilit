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
const ORNEK = vectors.cases[0];

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

test('ilk acilista PIN kurulumu istenir', async () => {
  await page.waitForSelector('#view-pin:not([hidden])');
  const aciklama = await page.textContent('#pin-aciklama');
  assert.match(aciklama, /PIN belirle/);
  assert.equal(await page.isHidden('#pin-input2'), false, 'PIN tekrar alani gorunmeli');
});

test('PIN belirlenince ana ekrana gecilir, tahta yok uyarisi cikar', async () => {
  await page.fill('#pin-input', '123456');
  await page.fill('#pin-input2', '123456');
  await page.click('#pin-form button[type=submit]');

  await page.waitForSelector('#view-home:not([hidden])');
  assert.equal(await page.isVisible('#tahta-yok'), true);
});

test('PIN tekrari uyusmazsa hata gosterilir', async () => {
  await page.click('#ayarlar-ac');
  await page.click('#pin-degistir');
  await page.waitForSelector('#view-pin:not([hidden])');

  await page.fill('#pin-input', '111111');
  await page.fill('#pin-input2', '222222');
  await page.click('#pin-form button[type=submit]');

  assert.match(await page.textContent('#pin-hata'), /aynı değil/i);

  // PIN'i degistirmeden devam et.
  await page.fill('#pin-input2', '111111');
  await page.click('#pin-form button[type=submit]');
  await page.waitForSelector('#view-home:not([hidden])');
});

test('eslesen tahta ana ekranda listelenir', async () => {
  // Kamera taklit edilemedigi icin eslestirme dogrudan kasaya yazilir.
  await page.evaluate(async ({ id, ad, k }) => {
    const { Vault } = await import('./store.js');
    const vault = await Vault.unlock('111111');
    await vault.addBoard({ boardId: id, name: ad, key: k });
  }, { id: ORNEK.boardId, ad: 'Z-Blok 204', k: ORNEK.key });

  await page.reload();
  await page.fill('#pin-input', '111111');
  await page.click('#pin-form button[type=submit]');

  await page.waitForSelector('#view-home:not([hidden])');
  assert.equal(await page.isVisible('#tahta-var'), true);
  assert.match(await page.textContent('#tahta-listesi'), /Z-Blok 204/);
});

test('elle girilen cagri dogru sifreyi uretir', async () => {
  await page.click('#elle-btn');
  await page.waitForSelector('#view-manual:not([hidden])');

  await page.selectOption('#manual-tahta', ORNEK.boardId);
  await page.fill('#manual-kod', ORNEK.challenge);
  await page.click('#manual-form button[type=submit]');

  await page.waitForSelector('#view-result:not([hidden])');

  const gosterilen = (await page.textContent('#sonuc-kod')).replace(/\s/g, '');
  assert.equal(gosterilen, ORNEK.response, 'Arayuzun urettigi sifre vektorle ayni olmali');
  assert.match(await page.textContent('#sonuc-tahta'), /Z-Blok 204/);
});

test('kucuk harfle ve bosluklu yazilan cagri da kabul edilir', async () => {
  await page.click('#sonuc-bitti');
  await page.click('#elle-btn');

  await page.selectOption('#manual-tahta', ORNEK.boardId);
  await page.fill('#manual-kod', ORNEK.challenge.toLowerCase().replace(/^(.{3})/, '$1 '));
  await page.click('#manual-form button[type=submit]');

  await page.waitForSelector('#view-result:not([hidden])');
  assert.equal((await page.textContent('#sonuc-kod')).replace(/\s/g, ''), ORNEK.response);
});

test('gecersiz cagri kodu reddedilir', async () => {
  await page.click('#sonuc-bitti');
  await page.click('#elle-btn');

  await page.fill('#manual-kod', 'ABC');
  await page.click('#manual-form button[type=submit]');

  assert.equal(await page.isVisible('#view-result'), false);
  assert.match(await page.textContent('#manual-hata'), /6 karakter/);
});

test('tahta silinince listeden kalkar', async () => {
  page.on('dialog', (d) => d.accept());

  await page.click('[data-geri]');
  await page.click('#ayarlar-ac');
  await page.click('#ayar-listesi .sil');

  await page.waitForFunction(() => !document.querySelector('#ayar-listesi .sil'));
  assert.equal(await page.textContent('#ayar-listesi'), '');
});

test('sayfada javascript hatasi olusmadi', () => {
  assert.deepEqual(page.hatalar, []);
});
