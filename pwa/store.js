// Telefondaki tahta anahtarlarinin saklanmasi.
//
// Anahtarlar duz halde durmaz: kullanicinin uygulama PIN'inden PBKDF2 ile
// turetilen bir AES-GCM anahtariyla sifrelenir. Telefon kaybolursa, PIN
// bilinmeden kasa okunamaz.

const DB_NAME = 'tahta-kilit';
const DB_VERSION = 1;
const STORE = 'kasa';
const VAULT_ID = 'vault';

const PBKDF2_ITERATIONS = 310000;
const SALT_LENGTH = 16;
const IV_LENGTH = 12;

function openDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(STORE)) {
        request.result.createObjectStore(STORE, { keyPath: 'id' });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function readRecord() {
  const db = await openDb();
  try {
    return await new Promise((resolve, reject) => {
      const request = db.transaction(STORE, 'readonly').objectStore(STORE).get(VAULT_ID);
      request.onsuccess = () => resolve(request.result ?? null);
      request.onerror = () => reject(request.error);
    });
  } finally {
    db.close();
  }
}

async function writeRecord(record) {
  const db = await openDb();
  try {
    await new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).put(record);
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}

async function deriveKey(pin, salt) {
  const material = await crypto.subtle.importKey(
    'raw', new TextEncoder().encode(pin), 'PBKDF2', false, ['deriveKey'],
  );

  return crypto.subtle.deriveKey(
    { name: 'PBKDF2', salt, iterations: PBKDF2_ITERATIONS, hash: 'SHA-256' },
    material,
    { name: 'AES-GCM', length: 256 },
    false,
    ['encrypt', 'decrypt'],
  );
}

/** Uygulama ilk kez mi aciliyor (henuz PIN belirlenmemis mi)? */
export async function isEmpty() {
  return (await readRecord()) === null;
}

/**
 * Acik kasa. Anahtarlar yalnizca bu nesne yasadigi surece bellekte durur;
 * uygulama kapaninca kaybolur, tekrar PIN gerekir.
 */
export class Vault {
  #key;
  #salt;
  #boards;

  constructor(key, salt, boards) {
    this.#key = key;
    this.#salt = salt;
    this.#boards = boards;
  }

  /** Yeni kasa olusturur (ilk kurulum). */
  static async create(pin) {
    const salt = crypto.getRandomValues(new Uint8Array(SALT_LENGTH));
    const vault = new Vault(await deriveKey(pin, salt), salt, []);
    await vault.#save();
    return vault;
  }

  /** Var olan kasayi acar. PIN yanlissa null doner. */
  static async unlock(pin) {
    const record = await readRecord();
    if (!record) return null;

    const salt = new Uint8Array(record.salt);
    const key = await deriveKey(pin, salt);

    try {
      const plain = await crypto.subtle.decrypt(
        { name: 'AES-GCM', iv: new Uint8Array(record.iv) }, key, new Uint8Array(record.data),
      );
      const boards = JSON.parse(new TextDecoder().decode(plain));
      return new Vault(key, salt, boards);
    } catch {
      return null; // yanlis PIN (veya bozuk kayit)
    }
  }

  /** Eslesmis tahtalar: {id, name, key(base64url)} */
  get boards() {
    return this.#boards.map((b) => ({ ...b }));
  }

  find(boardId) {
    return this.#boards.find((b) => b.id === boardId) ?? null;
  }

  /** Tahta ekler; ayni kimlik varsa anahtari ve adi gunceller. */
  async addBoard({ boardId, name, key }) {
    const entry = { id: boardId, name, key };
    const index = this.#boards.findIndex((b) => b.id === boardId);

    if (index >= 0) this.#boards[index] = entry;
    else this.#boards.push(entry);

    await this.#save();
  }

  async removeBoard(boardId) {
    this.#boards = this.#boards.filter((b) => b.id !== boardId);
    await this.#save();
  }

  async renameBoard(boardId, name) {
    const board = this.#boards.find((b) => b.id === boardId);
    if (board) {
      board.name = name;
      await this.#save();
    }
  }

  /** PIN'i degistirir: kasa yeni anahtarla bastan sifrelenir. */
  async changePin(newPin) {
    this.#salt = crypto.getRandomValues(new Uint8Array(SALT_LENGTH));
    this.#key = await deriveKey(newPin, this.#salt);
    await this.#save();
  }

  async #save() {
    const iv = crypto.getRandomValues(new Uint8Array(IV_LENGTH));
    const plain = new TextEncoder().encode(JSON.stringify(this.#boards));
    const data = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, this.#key, plain);

    await writeRecord({
      id: VAULT_ID,
      salt: Array.from(this.#salt),
      iv: Array.from(iv),
      data: Array.from(new Uint8Array(data)),
    });
  }
}
