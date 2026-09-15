// Tahta Kilit — cagri-cevap protokolu (telefon tarafi).
//
// src/TahtaKilit.Core/UnlockProtocol.cs ile birebir ayni hesabi yapar.
// Ikisinin uyumu spec/vectors.json uzerinden her iki tarafta da test edilir.
// Bu dosyayi degistirirsen C# tarafini da degistirmen gerekir.

export const VERSION = 'TK1';
export const KEY_LENGTH = 32;
export const CHALLENGE_LENGTH = 6;
export const RESPONSE_DIGITS = 8;
export const BOARD_ID_LENGTH = 8;

const ALPHABET = '0123456789ABCDEFGHJKMNPQRSTVWXYZ';
const RESPONSE_MODULUS = 100000000; // 10^8

/**
 * Kullanicinin yazdigi kodu tekillestirir: kucuk harf buyutulur, bosluk ve
 * tire atilir, karistirilan harfler duzeltilir (I/L -> 1, O -> 0).
 * Gecersizse null doner.
 */
export function normalizeCode(input, expectedLength) {
  if (typeof input !== 'string') return null;

  let out = '';
  for (const raw of input) {
    if (raw === ' ' || raw === '-' || raw === '\t' || raw === '_') continue;

    let c = raw.toUpperCase();
    if (c === 'I' || c === 'L') c = '1';
    else if (c === 'O') c = '0';

    if (!ALPHABET.includes(c)) return null;
    if (out.length === expectedLength) return null; // beklenenden uzun
    out += c;
  }

  return out.length === expectedLength ? out : null;
}

/** Girilen cevabi tekillestirir; tam RESPONSE_DIGITS haneli rakam olmali. */
export function normalizeResponse(entered) {
  if (typeof entered !== 'string') return null;

  let out = '';
  for (const c of entered) {
    if (c === ' ' || c === '-' || c === '\t') continue;
    if (c < '0' || c > '9') return null;
    if (out.length === RESPONSE_DIGITS) return null;
    out += c;
  }

  return out.length === RESPONSE_DIGITS ? out : null;
}

/** 32 baytlik base64url anahtari ham baytlara cevirir. */
export function decodeKey(value) {
  const padded = value.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(padded + '='.repeat((4 - (padded.length % 4)) % 4));
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

export function encodeKey(bytes) {
  let binary = '';
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
}

/**
 * Cagriya karsilik gelen 8 haneli cevabi hesaplar.
 * @param {Uint8Array} key 32 baytlik tahta anahtari
 * @param {string} boardId tahta kimligi
 * @param {string} challenge tahtanin gosterdigi 6 karakterlik cagri
 * @returns {Promise<string>} 8 haneli cevap
 */
export async function computeResponse(key, boardId, challenge) {
  if (!(key instanceof Uint8Array) || key.length !== KEY_LENGTH) {
    throw new Error(`Anahtar ${KEY_LENGTH} bayt olmali.`);
  }

  const id = normalizeCode(boardId, BOARD_ID_LENGTH);
  if (id === null) throw new Error('Gecersiz tahta kimligi.');

  const c = normalizeCode(challenge, CHALLENGE_LENGTH);
  if (c === null) throw new Error('Gecersiz cagri kodu.');

  const message = new TextEncoder().encode(`${VERSION}:${id}:${c}`);
  const cryptoKey = await crypto.subtle.importKey(
    'raw', key, { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'],
  );
  const mac = new Uint8Array(await crypto.subtle.sign('HMAC', cryptoKey, message));

  // RFC 4226 dinamik kesme — C# tarafiyla ayni.
  const offset = mac[mac.length - 1] & 0x0f;
  const truncated =
    ((mac[offset] & 0x7f) * 0x1000000) +
    (mac[offset + 1] * 0x10000) +
    (mac[offset + 2] * 0x100) +
    mac[offset + 3];

  return String(truncated % RESPONSE_MODULUS).padStart(RESPONSE_DIGITS, '0');
}

/** Cevabi ekranda okunakli gosterir: "40 82 17 55". */
export function formatForDisplay(response) {
  return response.replace(/(\d{2})(?=\d)/g, '$1 ');
}

/** Kilit ekranindaki QR icerigini cozer. */
export function parseChallengeQr(text) {
  let data;
  try {
    data = JSON.parse(text);
  } catch {
    return null;
  }

  if (!data || data.v !== 1 || typeof data.id !== 'string' || typeof data.c !== 'string') {
    return null;
  }

  const id = normalizeCode(data.id, BOARD_ID_LENGTH);
  const challenge = normalizeCode(data.c, CHALLENGE_LENGTH);
  return id && challenge ? { boardId: id, challenge } : null;
}

/** Eslestirme QR icerigini cozer. */
export function parsePairingQr(text) {
  let data;
  try {
    data = JSON.parse(text);
  } catch {
    return null;
  }

  if (!data || data.v !== 1 || typeof data.id !== 'string' ||
      typeof data.ad !== 'string' || typeof data.k !== 'string') {
    return null;
  }

  const id = normalizeCode(data.id, BOARD_ID_LENGTH);
  if (!id) return null;

  let key;
  try {
    key = decodeKey(data.k);
  } catch {
    return null;
  }

  return key.length === KEY_LENGTH ? { boardId: id, name: data.ad, key } : null;
}
