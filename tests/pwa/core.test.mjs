// pwa/core.js testleri.  Calistirmak icin:  node --test pwa/test/
//
// Ayni vektorler C# tarafinda da kosuluyor (tests/TahtaKilit.Core.Tests).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import {
  computeResponse, decodeKey, encodeKey, formatForDisplay,
  normalizeCode, normalizeResponse, parseChallengeQr, parsePairingQr,
} from '../../pwa/core.js';

const vectors = JSON.parse(
  readFileSync(new URL('../../spec/vectors.json', import.meta.url), 'utf8'),
);

for (const v of vectors.cases) {
  test(`vektor: ${v.note} (${v.challenge})`, async () => {
    const actual = await computeResponse(decodeKey(v.key), v.boardId, v.challenge);
    assert.equal(actual, v.response);
  });
}

test('cevap sekiz hanedir', async () => {
  const key = decodeKey(vectors.cases[0].key);
  const response = await computeResponse(key, 'ABCDEFGH', '4F7K2Q');
  assert.match(response, /^\d{8}$/);
});

test('farkli cagri farkli cevap verir', async () => {
  const key = decodeKey(vectors.cases[0].key);
  assert.notEqual(
    await computeResponse(key, 'ABCDEFGH', '4F7K2Q'),
    await computeResponse(key, 'ABCDEFGH', '4F7K2R'),
  );
});

test('cevap tahtaya baglidir', async () => {
  const key = decodeKey(vectors.cases[0].key);
  assert.notEqual(
    await computeResponse(key, 'ABCDEFGH', '4F7K2Q'),
    await computeResponse(key, 'ABCDEFGJ', '4F7K2Q'),
  );
});

test('kucuk harf ve karistirilan harfler duzeltilir', async () => {
  const key = decodeKey(vectors.cases[0].key);
  assert.equal(
    await computeResponse(key, 'abcdefgh', '4f7k2q'),
    await computeResponse(key, 'ABCDEFGH', '4F7K2Q'),
  );
});

test('gecersiz anahtar uzunlugu reddedilir', async () => {
  await assert.rejects(() => computeResponse(new Uint8Array(16), 'ABCDEFGH', '4F7K2Q'));
});

test('gecersiz cagri reddedilir', async () => {
  const key = decodeKey(vectors.cases[0].key);
  await assert.rejects(() => computeResponse(key, 'ABCDEFGH', '4F7K2'));
  await assert.rejects(() => computeResponse(key, 'ABCDEFGH', '4F7K2U'));
});

test('kod tekillestirme', () => {
  assert.equal(normalizeCode('4f7k-2q', 6), '4F7K2Q');
  assert.equal(normalizeCode('4F7K2O', 6), '4F7K20');
  assert.equal(normalizeCode('4F7KIQ', 6), '4F7K1Q');
  assert.equal(normalizeCode('4F7KLQ', 6), '4F7K1Q');
  assert.equal(normalizeCode('4F7K2', 6), null);
  assert.equal(normalizeCode('4F7K2QQ', 6), null);
  assert.equal(normalizeCode('4F7K2U', 6), null);
  assert.equal(normalizeCode(null, 6), null);
});

test('cevap tekillestirme', () => {
  assert.equal(normalizeResponse('42 63 04 48'), '42630448');
  assert.equal(normalizeResponse('4263-0448'), '42630448');
  assert.equal(normalizeResponse('4263044'), null);
  assert.equal(normalizeResponse('426304489'), null);
  assert.equal(normalizeResponse('4263O448'), null);
});

test('ekran bicimi ikiserli gruplar', () => {
  assert.equal(formatForDisplay('42630448'), '42 63 04 48');
});

test('anahtar kodlama gidis donus', () => {
  const bytes = crypto.getRandomValues(new Uint8Array(32));
  const encoded = encodeKey(bytes);
  assert.ok(!/[+/=]/.test(encoded));
  assert.deepEqual(decodeKey(encoded), bytes);
});

test('kilit ekrani QR icerigi cozulur', () => {
  const parsed = parseChallengeQr('{"v":1,"id":"ABCDEFGH","c":"4F7K2Q"}');
  assert.deepEqual(parsed, { boardId: 'ABCDEFGH', challenge: '4F7K2Q' });
});

test('eslestirme QR icerigi cozulur', () => {
  const key = crypto.getRandomValues(new Uint8Array(32));
  const json = JSON.stringify({ v: 1, id: 'ABCDEFGH', ad: 'Z-Blok 204', k: encodeKey(key) });
  const parsed = parsePairingQr(json);
  assert.equal(parsed.boardId, 'ABCDEFGH');
  assert.equal(parsed.name, 'Z-Blok 204');
  assert.deepEqual(parsed.key, key);
});

test('bozuk QR icerigi null doner', () => {
  assert.equal(parseChallengeQr('bu json degil'), null);
  assert.equal(parseChallengeQr('{"v":2,"id":"ABCDEFGH","c":"4F7K2Q"}'), null);
  assert.equal(parseChallengeQr('{"v":1,"id":"KISA","c":"4F7K2Q"}'), null);
  assert.equal(parsePairingQr('{"v":1,"id":"ABCDEFGH","ad":"X","k":"kisa"}'), null);
});
