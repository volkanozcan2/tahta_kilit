// pwa/core.js testleri.  Calistirmak icin:  node --test tests/pwa/*.test.mjs
//
// Ayni vektorler C# tarafinda da kosuluyor (tests/TahtaKilit.Core.Tests).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import {
  formatAnswer, formatCode, normalizeAnswer, normalizeCode, parseQr, solve,
  ANSWER_DIGITS, CODE_DIGITS,
} from '../../pwa/core.js';

const vectors = JSON.parse(
  readFileSync(new URL('../../spec/vectors.json', import.meta.url), 'utf8'),
);

for (const v of vectors.cases) {
  test(`vektor: ${v.not} (${v.kod})`, () => {
    assert.equal(solve(v.kod), v.cevap);
  });
}

test('cevap ilk ve son yarimin xoru', () => {
  assert.equal(solve('123456654321'), String(123456 ^ 654321).padStart(7, '0'));
});

test('cevap hep yedi hanedir', () => {
  assert.equal(solve('999999999999'), '0000000');
  assert.equal(solve('000001000002').length, ANSWER_DIGITS);
});

test('xor simetriktir', () => {
  assert.equal(solve('123456654321'), solve('654321123456'));
});

test('sifirla xor degistirmez', () => {
  assert.equal(solve('000000123456'), '0123456');
  assert.equal(solve('123456000000'), '0123456');
});

test('bozuk kod reddedilir', () => {
  assert.throws(() => solve('12345665432'));
  assert.throws(() => solve('1234566543210'));
  assert.throws(() => solve('12345665432A'));
  assert.throws(() => solve(''));
});

test('kod tekillestirme', () => {
  assert.equal(normalizeCode('1234 5665 4321'), '123456654321');
  assert.equal(normalizeCode('1234-5665-4321'), '123456654321');
  assert.equal(normalizeCode('12345665432'), null);
  assert.equal(normalizeCode('1234566543210'), null);
  assert.equal(normalizeCode(null), null);
});

test('cevap tekillestirme', () => {
  assert.equal(normalizeAnswer('053 0865'), '0530865');
  assert.equal(normalizeAnswer('530865'), null);
  assert.equal(normalizeAnswer('05308650'), null);
});

test('ekran bicimleri okunakli', () => {
  assert.equal(formatCode('123456654321'), '1234 5665 4321');
  assert.equal(formatAnswer('0530865'), '053 0865');
});

test('karekod icerigi cozulur', () => {
  assert.equal(parseQr('123456654321'), '123456654321');
  assert.equal(parseQr('  123456654321  '), '123456654321');
  assert.equal(parseQr('bu kod degil'), null);
  assert.equal(parseQr('{"v":1}'), null);
});

test('her kod icin cevap uretilebiliyor', () => {
  // Basa sifirli, tavan ve taban degerler dahil hicbir kod patlamamali.
  for (const kod of ['000000000000', '999999999999', '000000999999', '999999000000']) {
    assert.match(solve(kod), new RegExp(`^\\d{${ANSWER_DIGITS}}$`));
  }
  assert.equal(CODE_DIGITS, 12);
});
