// Tahta Kilit — telefon uygulamasi arayuzu.
//
// Tek isi var: tahtadaki karekodu okuyup kilit sifresini hesaplamak.
// Eslestirme, hesap veya saklanan hicbir veri yok.

import { formatAnswer, formatCode, normalizeCode, parseQr, solve, CODE_DIGITS } from './core.js';

const $ = (id) => document.getElementById(id);

let kameraDurdur = null;

// ---------------------------------------------------------------- gorunumler

const GORUNUMLER = ['home', 'scan', 'manual', 'result'];

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

// -------------------------------------------------------------------- kamera

async function kamerayiAc() {
  hata('scan-hata', null);
  goster('scan');

  const video = $('kamera');
  let stream;

  try {
    stream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: 'environment' }, audio: false,
    });
  } catch {
    hata('scan-hata', 'Kameraya erişilemedi. Aşağıdan kodu elle girebilirsin.');
    return;
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

  const tara = () => {
    if (!calisiyor) return;

    if (video.readyState === video.HAVE_ENOUGH_DATA) {
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

      const kare = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const okunan = jsQR(kare.data, kare.width, kare.height, { inversionAttempts: 'dontInvert' });

      if (okunan) {
        const kod = parseQr(okunan.data);

        if (kod) {
          kamerayiKapat();
          sonucGoster(kod);
          return;
        }

        hata('scan-hata', 'Bu karekod Tahta Kilit karekodu değil.');
      }
    }

    requestAnimationFrame(tara);
  };

  requestAnimationFrame(tara);
}

function kamerayiKapat() {
  kameraDurdur?.();
}

// ----------------------------------------------------------- elle kod girisi

function elleEkrani() {
  $('manual-kod').value = '';
  hata('manual-hata', null);
  goster('manual');
  $('manual-kod').focus();
}

// Yazarken dortlu gruplara ayir: 1234 5665 4321
$('manual-kod').addEventListener('input', (e) => {
  const rakamlar = e.target.value.replace(/\D/g, '').slice(0, CODE_DIGITS);
  e.target.value = formatCode(rakamlar);
});

$('manual-form').addEventListener('submit', (e) => {
  e.preventDefault();

  const kod = normalizeCode($('manual-kod').value);
  if (!kod) {
    hata('manual-hata', `Sayı ${CODE_DIGITS} haneli olmalı. Tahtadaki sayıyı olduğu gibi yaz.`);
    return;
  }

  sonucGoster(kod);
});

// -------------------------------------------------------------------- sonuc

function sonucGoster(kod) {
  $('sonuc-kod').textContent = formatAnswer(solve(kod));
  goster('result');
}

// ------------------------------------------------------------------ baglanti

$('ac-btn').addEventListener('click', kamerayiAc);
$('elle-btn').addEventListener('click', elleEkrani);
$('scan-elle').addEventListener('click', elleEkrani);
$('scan-iptal').addEventListener('click', () => goster('home'));
$('sonuc-bitti').addEventListener('click', () => goster('home'));

for (const btn of document.querySelectorAll('[data-geri]')) {
  btn.addEventListener('click', () => goster('home'));
}

// Uygulama arka plana atilirsa kamerayi birak (pil ve gizlilik).
document.addEventListener('visibilitychange', () => {
  if (document.hidden) kamerayiKapat();
});

if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('sw.js').catch(() => { /* cevrimdisi calisma devre disi */ });
}

goster('home');
