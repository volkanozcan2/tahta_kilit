// Ortak test vektorlerini uretir: spec/vectors.json
//
// Vektorler hem C# hem JS tarafinda kosulur; iki uygulamanin ayni cevabi
// verdigini garanti eder. Protokol degisirse: node spec/generate-vectors.mjs

import { writeFileSync } from 'node:fs';
import { solve } from '../pwa/core.js';

const kodlar = [
  { kod: '123456654321', not: 'temel durum' },
  { kod: '000000000000', not: 'hepsi sifir' },
  { kod: '999999999999', not: 'ayni yarimlar, sonuc sifir' },
  { kod: '000000999999', not: 'ilk yarim sifir' },
  { kod: '999999000000', not: 'son yarim sifir' },
  { kod: '000001000002', not: 'en kucuk fark' },
  { kod: '524287524288', not: 'bit siniri civari' },
  { kod: '000015000240', not: 'ayrik bitler' },
];

const cases = kodlar.map(({ kod, not }) => ({ not, kod, cevap: solve(kod) }));

writeFileSync(
  new URL('./vectors.json', import.meta.url),
  JSON.stringify({
    aciklama: 'Tahta Kilit XOR test vektorleri. spec/generate-vectors.mjs ile uretilir.',
    cases,
  }, null, 2) + '\n',
);

console.log(`${cases.length} vektor yazildi.`);
