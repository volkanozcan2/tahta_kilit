// Tahta Kilit — telefon uygulamasi arayuzu.

import { computeResponse, decodeKey, encodeKey, formatForDisplay, normalizeCode,
         parseChallengeQr, parsePairingQr, CHALLENGE_LENGTH } from './core.js';
import { isEmpty, Vault } from './store.js';

const $ = (id) => document.getElementById(id);

let vault = null;
let geriHedef = 'home';
let kameraDurdur = null;

// ---------------------------------------------------------------- gorunumler

const GORUNUMLER = ['pin', 'home', 'scan', 'manual', 'result', 'settings'];

function goster(ad) {
  if (ad !== 'scan') kamerayiKapat();

  for (const g of GORUNUMLER) {
    $(`view-${g}`).hidden = g !== ad;
  }
}

function hata(elemanId, mesaj) {
  const el = $(elemanId);
  el.textContent = mesaj ?? '';
  el.hidden = !mesaj;
}

// ------------------------------------------------------------------ PIN akisi

// 'kur' (ilk kurulum) | 'ac' (acilis) | 'degistir' (PIN degisimi)
let pinModu = 'ac';

function pinEkrani(mod) {
  pinModu = mod;
  $('pin-aciklama').textContent = {
    kur: 'Uygulamayı korumak için bir PIN belirle. Telefonun kaybolursa tahta anahtarları bu PIN olmadan okunamaz.',
    ac: 'Devam etmek için PIN’ini gir.',
    degistir: 'Yeni PIN belirle.',
  }[mod];

  $('pin-input2').hidden = mod === 'ac';
  $('pin-input').value = '';
  $('pin-input2').value = '';
  hata('pin-hata', null);
  goster('pin');
  $('pin-input').focus();
}

$('pin-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const pin = $('pin-input').value;

  if (pinModu === 'ac') {
    vault = await Vault.unlock(pin);
    if (!vault) return hata('pin-hata', 'PIN yanlış.');
    return anaEkran();
  }

  if (pin.length < 4) return hata('pin-hata', 'PIN en az 4 haneli olmalı.');
  if (pin !== $('pin-input2').value) return hata('pin-hata', 'İki PIN aynı değil.');

  if (pinModu === 'kur') vault = await Vault.create(pin);
  else await vault.changePin(pin);

  anaEkran();
});

// ------------------------------------------------------------------ ana ekran

function anaEkran() {
  geriHedef = 'home';
  const tahtalar = vault.boards;
  $('tahta-yok').hidden = tahtalar.length > 0;
  $('tahta-var').hidden = tahtalar.length === 0;

  $('tahta-listesi').innerHTML = '';
  for (const t of tahtalar) {
    const li = document.createElement('li');
    li.innerHTML = '<span class="ad"></span><span class="kimlik"></span>';
    li.querySelector('.ad').textContent = t.name;
    li.querySelector('.kimlik').textContent = t.id;
    $('tahta-listesi').append(li);
  }

  goster('home');
}

$('ac-btn').addEventListener('click', () => kamerayiAc('unlock'));
$('elle-btn').addEventListener('click', elleEkrani);
$('ilk-esle').addEventListener('click', () => kamerayiAc('pair'));
$('ayarlar-ac').addEventListener('click', ayarlarEkrani);
$('sonuc-bitti').addEventListener('click', anaEkran);

for (const btn of document.querySelectorAll('[data-geri]')) {
  btn.addEventListener('click', () => (geriHedef === 'settings' ? ayarlarEkrani() : anaEkran()));
}

// -------------------------------------------------------------------- kamera

async function kamerayiAc(mod) {
  hata('scan-hata', null);
  $('scan-aciklama').textContent = mod === 'pair'
    ? 'Tahtadaki eşleştirme karekodunu okut'
    : 'Tahtadaki karekodu okut';
  $('scan-elle').hidden = mod === 'pair';
  goster('scan');

  const video = $('kamera');
  let stream;

  try {
    stream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: 'environment' }, audio: false,
    });
  } catch {
    return hata('scan-hata',
      mod === 'pair'
        ? 'Kameraya erişilemedi. Tarayıcı ayarlarından kamera iznini aç.'
        : 'Kameraya erişilemedi. Aşağıdan kodu elle girebilirsin.');
  }

  video.srcObject = stream;
  await video.play();

  const canvas = $('tuval');
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  let calisiyor = true;

  kameraDurdur = () => {
    calisiyor = false;
    for (const track of stream.getTracks()) track.stop();
    video.srcObject = null;
    kameraDurdur = null;
  };

  const tara = async () => {
    if (!calisiyor) return;

    if (video.readyState === video.HAVE_ENOUGH_DATA) {
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

      const kare = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const kod = jsQR(kare.data, kare.width, kare.height, { inversionAttempts: 'dontInvert' });

      if (kod) {
        const islendi = mod === 'pair'
          ? await eslestirmeyiIsle(kod.data)
          : await acmaKodunuIsle(kod.data);

        if (islendi) return; // kamera kapandi, ekran degisti
      }
    }

    requestAnimationFrame(tara);
  };

  requestAnimationFrame(tara);
}

function kamerayiKapat() {
  kameraDurdur?.();
}

$('scan-iptal').addEventListener('click', () => (geriHedef === 'settings' ? ayarlarEkrani() : anaEkran()));
$('scan-elle').addEventListener('click', elleEkrani);

async function acmaKodunuIsle(metin) {
  const okunan = parseChallengeQr(metin);
  if (!okunan) {
    hata('scan-hata', 'Bu karekod Tahta Kilit karekodu değil.');
    return false;
  }

  const tahta = vault.find(okunan.boardId);
  if (!tahta) {
    hata('scan-hata', 'Bu tahta telefonunda eşleşmemiş. Ayarlardan ekleyebilirsin.');
    return false;
  }

  kamerayiKapat();
  await sonucGoster(tahta, okunan.challenge);
  return true;
}

async function eslestirmeyiIsle(metin) {
  const okunan = parsePairingQr(metin);
  if (!okunan) {
    hata('scan-hata', 'Bu karekod eşleştirme karekodu değil.');
    return false;
  }

  kamerayiKapat();
  await vault.addBoard({
    boardId: okunan.boardId,
    name: okunan.name || okunan.boardId,
    key: encodeKey(okunan.key),
  });

  geriHedef === 'settings' ? ayarlarEkrani() : anaEkran();
  return true;
}

// ----------------------------------------------------------- elle kod girisi

function elleEkrani() {
  const tahtalar = vault.boards;
  const secim = $('manual-tahta');

  secim.innerHTML = '';
  for (const t of tahtalar) {
    const opt = document.createElement('option');
    opt.value = t.id;
    opt.textContent = t.name;
    secim.append(opt);
  }

  $('manual-kod').value = '';
  hata('manual-hata', null);
  goster('manual');
}

$('manual-form').addEventListener('submit', async (e) => {
  e.preventDefault();

  const tahta = vault.find($('manual-tahta').value);
  if (!tahta) return hata('manual-hata', 'Önce bir tahta seç.');

  const cagri = normalizeCode($('manual-kod').value, CHALLENGE_LENGTH);
  if (!cagri) return hata('manual-hata', 'Kod 6 karakter olmalı. Tahtadaki kodu olduğu gibi yaz.');

  await sonucGoster(tahta, cagri);
});

// -------------------------------------------------------------------- sonuc

async function sonucGoster(tahta, cagri) {
  const cevap = await computeResponse(decodeKey(tahta.key), tahta.id, cagri);
  $('sonuc-tahta').textContent = tahta.name;
  $('sonuc-kod').textContent = formatForDisplay(cevap);
  goster('result');
}

// ------------------------------------------------------------------ ayarlar

function ayarlarEkrani() {
  geriHedef = 'settings';
  $('ayar-listesi').innerHTML = '';

  for (const t of vault.boards) {
    const li = document.createElement('li');
    li.innerHTML = '<span class="ad"></span><button class="sil" type="button">Sil</button>';
    li.querySelector('.ad').textContent = t.name;
    li.querySelector('.sil').addEventListener('click', async () => {
      if (!confirm(`"${t.name}" silinsin mi? Bu tahtayı bir daha açamazsın.`)) return;
      await vault.removeBoard(t.id);
      ayarlarEkrani();
    });
    $('ayar-listesi').append(li);
  }

  goster('settings');
}

$('tahta-ekle').addEventListener('click', () => kamerayiAc('pair'));
$('pin-degistir').addEventListener('click', () => pinEkrani('degistir'));

// -------------------------------------------------------------------- acilis

// Uygulama arka plana atilirsa kamerayi birak (pil ve gizlilik).
document.addEventListener('visibilitychange', () => {
  if (document.hidden) kamerayiKapat();
});

async function baslat() {
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('sw.js').catch(() => { /* cevrimdisi calisma devre disi */ });
  }

  geriHedef = 'home';
  pinEkrani(await isEmpty() ? 'kur' : 'ac');
}

baslat();
