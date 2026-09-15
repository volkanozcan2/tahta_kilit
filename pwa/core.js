// Tahta Kilit — kilit acma kurali (telefon tarafi).
//
// src/TahtaKilit.Core/XorProtocol.cs ile birebir ayni hesabi yapar.
// Ikisinin uyumu spec/vectors.json uzerinden her iki tarafta da test edilir.
//
// DIKKAT — bu kuralda gizli anahtar yoktur. Kilidin cevabi, tahtanin ekraninda
// gosterilen sayidan hesaplanir; kurali bilen herkes ayni sonucu bulabilir.
// Bilerek boyle secildi (bkz. docs/TASARIM.md).

export const CODE_DIGITS = 12;
export const HALF_DIGITS = CODE_DIGITS / 2;
export const ANSWER_DIGITS = 7;

/**
 * Koddan cevabi hesaplar: ilk yarim XOR son yarim.
 * @param {string} code tahtanin gosterdigi 12 haneli sayi
 * @returns {string} 7 haneli cevap
 */
export function solve(code) {
  const normalized = normalizeCode(code);
  if (normalized === null) {
    throw new Error(`Kod ${CODE_DIGITS} haneli olmali.`);
  }

  const ilk = Number(normalized.slice(0, HALF_DIGITS));
  const son = Number(normalized.slice(HALF_DIGITS));

  return String(ilk ^ son).padStart(ANSWER_DIGITS, '0');
}

/** Karekodda gosterilen sayiyi tekillestirir; gecersizse null doner. */
export function normalizeCode(code) {
  return normalizeDigits(code, CODE_DIGITS);
}

/** Girilen cevabi tekillestirir; gecersizse null doner. */
export function normalizeAnswer(entered) {
  return normalizeDigits(entered, ANSWER_DIGITS);
}

function normalizeDigits(input, expectedLength) {
  if (typeof input !== 'string') return null;

  let out = '';
  for (const c of input) {
    if (c === ' ' || c === '-' || c === '\t') continue;
    if (c < '0' || c > '9') return null;
    if (out.length === expectedLength) return null;
    out += c;
  }

  return out.length === expectedLength ? out : null;
}

/** Ekranda okunakli gosterim: "1234 5665 4321". */
export function formatCode(code) {
  return code.replace(/(\d{4})(?=\d)/g, '$1 ');
}

/** Cevabi okunakli gosterir: "053 7777". */
export function formatAnswer(answer) {
  return answer.length === ANSWER_DIGITS
    ? `${answer.slice(0, 3)} ${answer.slice(3)}`
    : answer;
}

/**
 * Karekodun icerigini cozer. Tahta karekoda yalnizca 12 haneli sayiyi
 * yazar; etrafinda bosluk olabilir diye tekillestirilir.
 */
export function parseQr(text) {
  return normalizeCode(typeof text === 'string' ? text.trim() : text);
}
