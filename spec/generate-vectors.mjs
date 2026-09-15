// Ortak test vektorlerini uretir: spec/vectors.json
//
// Vektorler hem C# (tests/TahtaKilit.Core.Tests) hem de JS (pwa/test) tarafinda
// kosulur. Iki bagimsiz uygulamanin ayni sonucu verdigini garanti eder.
// Protokol degisirse: node spec/generate-vectors.mjs

import { writeFileSync } from 'node:fs';
import { computeResponse, encodeKey, VERSION } from '../pwa/core.js';

// Sabit, tahmin edilebilir anahtarlar — sadece test icin.
function testKey(seed) {
  const key = new Uint8Array(32);
  for (let i = 0; i < 32; i++) key[i] = (seed * 37 + i * 11) & 0xff;
  return key;
}

const inputs = [
  { key: testKey(1), boardId: 'ABCDEFGH', challenge: '4F7K2Q', note: 'temel durum' },
  { key: testKey(2), boardId: '00000000', challenge: '000000', note: 'en kucuk degerler' },
  { key: testKey(3), boardId: 'ZZZZZZZZ', challenge: 'ZZZZZZ', note: 'en buyuk degerler' },
  { key: testKey(4), boardId: 'TK3M9WP2', challenge: 'H8N4TV', note: 'karisik' },
  { key: testKey(5), boardId: 'QRSTVWXY', challenge: '9BCDFG', note: 'karisik' },
];

const cases = [];
for (const input of inputs) {
  cases.push({
    note: input.note,
    key: encodeKey(input.key),
    boardId: input.boardId,
    challenge: input.challenge,
    response: await computeResponse(input.key, input.boardId, input.challenge),
  });
}

// Basa sifir gelen bir durum da bulunsun: dolgulama hatasi sessizce gecmesin.
const zeroKey = testKey(6);
for (let i = 0; i < 100000; i++) {
  const challenge = String(i).padStart(6, '0').replace(/[^0-9]/g, '0');
  const response = await computeResponse(zeroKey, 'ZEROPAD1', challenge);
  if (response.startsWith('0')) {
    cases.push({
      note: 'cevap sifirla basliyor (dolgulama testi)',
      key: encodeKey(zeroKey),
      boardId: 'ZEROPAD1',
      challenge,
      response,
    });
    break;
  }
}

const vectors = {
  version: VERSION,
  aciklama: 'Tahta Kilit cagri-cevap test vektorleri. spec/generate-vectors.mjs ile uretilir.',
  cases,
};

writeFileSync(new URL('./vectors.json', import.meta.url), JSON.stringify(vectors, null, 2) + '\n');
console.log(`${cases.length} vektor yazildi.`);
