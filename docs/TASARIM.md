# Tahta Kilit — Tasarım Kararları

Windows 10 akıllı tahtaları kilitleyen, yalnızca öğretmenin telefonundaki
uygulamadan üretilen tek kullanımlık şifreyle açılan sistem.

Bu belge, uygulamaya başlamadan önce verilen kararların kaydıdır.

---

## 1. Özet

| Konu | Karar |
|---|---|
| Kilit yöntemi | Kiosk overlay (tam ekran, her zaman üstte) + Windows servisi watchdog |
| Şifre modeli | Çağrı–cevap (challenge–response), HMAC-SHA256 — **saatten ve internetten bağımsız** |
| Şifre giriş yolu | Tahtadaki QR'ı telefonla okut **veya** 6 haneli çağrı kodunu telefona elle gir |
| Sunucu | Yok. Her şey tahtada ve telefonda yerel |
| Mobil taraf | PWA (telefona kurulabilen web uygulaması), çevrimdışı çalışır |
| Kilitlenme anı | Her Windows açılışında + ders saati takvimine göre |
| Eşleştirme | Bir telefon birden çok tahtayı açabilir; her tahtanın kendi anahtarı var |
| Kurulum | Yönetici kurulum sihirbazı (MSI/EXE), kurulum PIN'i ile korunur |

---

## 2. Neden TOTP değil?

İlk akla gelen çözüm Google Authenticator tarzı TOTP'dir, ancak sahadaki iki
gerçek bunu eliyor:

- **Tahtaların saati kayıyor.** TOTP 30 saniyelik zaman dilimlerine dayanır;
  tahtanın saati birkaç dakika şaşarsa doğru kod bile reddedilir.
- **İnternet kopuyor.** Saat sunucudan (NTP) düzeltilemiyor, merkezi
  doğrulama da yapılamıyor.

Bunun yerine **çağrı–cevap** kullanıyoruz: zaman hiç denkleme girmez.

### Akış

```
   TAHTA (kilitli)                        TELEFON (PWA)
   ──────────────                         ─────────────
1. Rastgele çağrı üretir  C
   Ekranda gösterir:
     ┌─────────────┐
     │   [ QR ]    │   ──── QR okut ────►  2. QR'dan tahta kimliği + C okunur
     │             │        (veya)            (kamera yoksa: 6 haneli kod elle)
     │  4F7K-2Q    │
     └─────────────┘                      3. O tahtanın anahtarı K ile:
                                             cevap = HMAC-SHA256(K, id‖C)
                                             → 8 haneli sayı

5. Aynı hesabı kendi de yapar,      ◄──── 4. Ekranda: 40 82 17 55
   eşleşiyorsa kilidi açar                    (öğretmen tahtaya yazar)
   ve C'yi "kullanıldı" işaretler
```

**Özellikler**

- Saat hiçbir adımda kullanılmaz → saat kayması sorunu ortadan kalkar.
- İnternet hiçbir adımda kullanılmaz → tahta ve telefon tamamen çevrimdışı.
- Her açılışta çağrı `C` yeniden üretilir → aynı kod ikinci kez işe yaramaz
  (omuz üstünden kodu gören öğrenci sonra kullanamaz).
- Telefon ile tahta arasında ağ bağlantısı gerekmez; köprü insanın gözü ve eli.

---

## 3. Kriptografi ayrıntıları

**Eşleştirme (bir kez, kurulumda)**
- Tahta 256 bit rastgele anahtar `K` üretir (`RNGCryptoServiceProvider`).
- Eşleştirme QR'ı: `{"v":1,"id":"<tahta-id>","ad":"Z-Blok 204","k":"<base64url K>"}`
- Telefon bu kaydı IndexedDB'ye yazar. Tahta anahtarı DPAPI ile şifreleyip
  `C:\ProgramData\TahtaKilit\` altında saklar (ACL: SYSTEM + Administrators).

**Kilit açma**
- Çağrı `C`: 30 bit rastgele → Crockford Base32 ile 6 karakter (`4F7K2Q`).
  Elle yazılabilecek kadar kısa, tahmin edilemeyecek kadar geniş
  (yanlış deneme sınırı ile birlikte).
- Cevap: `HMAC-SHA256(K, id ‖ ":" ‖ C)` → ilk 4 bayt → `mod 10^8` → 8 hane,
  ekranda `40 82 17 55` biçiminde gruplanır.
- Doğrulama sabit zamanlı karşılaştırma ile yapılır.
- Kabul edilen çağrı "kullanıldı" olarak işaretlenir, yeni çağrı üretilir.

**Yanlış deneme koruması**
- 5 yanlış denemeden sonra artan bekleme (10 sn → 30 sn → 2 dk → 5 dk).
- Bekleme süresi sistem saatine değil, açılıştan beri geçen süreye
  (`Environment.TickCount64`) dayanır — saat ileri alınarak atlatılamaz.
- Tüm denemeler yerel log dosyasına yazılır.

**Dürüst sınır:** Tahtada yönetici yetkisi olan biri diski okuyarak `K`
anahtarını çıkarabilir. Bu sistem, yetkisiz *kullanıma* karşı koruma sağlar;
kararlı bir saldırgana veya donanıma fiziksel erişime karşı değil.

---

## 4. Windows tarafı mimarisi

Üç parça, .NET 8 (Windows 10 1809+ uyumlu, self-contained yayın):

**`TahtaKilit.Service`** — Windows servisi, SYSTEM olarak çalışır
- Açılışta kilit durumunu belirler (varsayılan: kilitli).
- Kilit ekranını aktif oturuma başlatır (`CreateProcessAsUser` + `WTSGetActiveConsoleSessionId`).
- Watchdog: kilit ekranı öldürülürse 1 saniye içinde yeniden başlatır.
- Oturum değişimlerini dinler (kullanıcı değişimi, uzak masaüstü).
- Ders saati takvimini uygular.

**`TahtaKilit.Lock`** — WPF tam ekran kilit ekranı, kullanıcı oturumunda
- Tüm ekranları kaplar (çoklu monitör), `Topmost`, görev çubuğu gizlenir.
- QR + 6 haneli çağrı kodu + 8 haneli cevap giriş alanı (dokunmatik tuş takımı).
- Düşük seviye klavye kancası (`WH_KEYBOARD_LL`) ile engellenenler:
  `Win`, `Alt+Tab`, `Alt+F4`, `Ctrl+Esc`, `Ctrl+Shift+Esc`, `Alt+Esc`.
- Ekranda okul/sınıf adı ve "BT'ye başvurun" bilgi metni.

**`TahtaKilit.Admin`** — kurulum sihirbazı ve yönetim aracı
- İlk çalıştırmada: tahta adı, kurulum PIN'i belirleme, anahtar üretimi.
- Eşleştirme QR'ını gösterir (yeni telefon eklemek her zaman PIN ister).
- Ders saati takvimi düzenleme, boşta kalma süresi, log görüntüleme.
- Kilidi kalıcı devre dışı bırakma / kaldırma (PIN ile).

### `Ctrl+Alt+Del` hakkında

Secure Attention Sequence, çekirdek sürücüsü olmadan engellenemez. Alınan
önlemler:
- Kilitliyken Görev Yöneticisi politika ile kapatılır
  (`DisableTaskMgr`), kilit açılınca eski haline döndürülür.
- Kullanıcı yine de Windows'un kendi kilit ekranına düşebilir; oturuma geri
  döndüğünde servis kilit ekranını hemen yeniden öne getirir.
- Bu, kararlı bir kullanıcının Güvenli Mod ile aşabileceği bir korumadır;
  hedef kitle (izinsiz kullanan öğrenci) için yeterlidir. Daha sıkı koruma
  gerekirse ikinci aşamada shell replacement'a geçilebilir.

---

## 5. Ders saati takvimi ve saat kayması

Takvim özelliği istendi, ancak tahtanın saati güvenilmez. Bu yüzden takvim
**yalnızca kilitleme yönünde** çalışır:

- Takvim tahtayı kilitleyebilir, **asla kendi başına açamaz.**
- Açmanın tek yolu her zaman telefondan gelen koddur.

Böylece saat şaşsa bile en kötü ihtimalle tahta beklenmedik anda kilitlenir —
öğretmen telefonuyla saniyeler içinde açar. Ters durumda (saat yüzünden
tahtanın kendiliğinden açık kalması) güvenlik açığı doğardı; bu tasarım onu
imkânsız kılar.

Ek olarak Admin ekranında "Saat sapmış görünüyor" uyarısı gösterilir
(son kapanış zamanından geriye gitmiş bir saat tespit edilirse).

**Kilitlenme tetikleri**
1. Her Windows açılışında (varsayılan, kapatılamaz).
2. Takvim dışı saatlerde (ör. 17:00–07:30 arası) — isteğe bağlı.
3. Elle kilitleme kısayolu — öğretmen çıkarken.

---

## 6. Telefon tarafı (PWA)

Mağaza derdi olmayan, telefona "Ana ekrana ekle" ile kurulan web uygulaması.

- **Teknoloji:** saf HTML/CSS/JS, çerçeve yok. `jsQR` ile kamera okuma,
  WebCrypto (`crypto.subtle`) ile HMAC-SHA256.
- **Çevrimdışı:** Service Worker tüm dosyaları önbelleğe alır. İlk kurulumdan
  sonra internet gerekmez.
- **Depolama:** tahta anahtarları IndexedDB'de. Uygulama açılışında ekran
  kilidi/PIN istenir (uygulama içi PIN, anahtarlar PIN'den türetilen anahtarla
  şifrelenir — telefon kaybolursa anahtarlar okunamaz).
- **Ekranlar:**
  1. Tahta listesi (eşleşmiş tahtalar, isimleriyle)
  2. "Tahta Aç" → kamera açılır, QR okutulur
  3. Kamera yoksa "Kodu elle gir" → 6 haneli çağrı kodu
  4. Sonuç ekranı: büyük puntolarla 8 haneli cevap
  5. Ayarlar: yeni tahta ekle (eşleştirme QR'ı okut), tahta sil, PIN değiştir
- **Barındırma:** statik dosyalar; HTTPS şart (kamera erişimi ve PWA kurulumu
  için). GitHub Pages veya Netlify yeterli. Sayfa yalnızca dağıtım içindir,
  hiçbir veri sunucuya gitmez.

---

## 7. Kurulum akışı

1. BT sorumlusu tahtaya `TahtaKilit-Setup.exe` kurar (yönetici hakkı ister).
2. Sihirbaz: tahta adı girilir → kurulum PIN'i belirlenir → anahtar üretilir.
3. Ekranda eşleştirme QR'ı çıkar.
4. Öğretmen telefonunda PWA'yı açar → "Yeni tahta ekle" → QR'ı okutur.
5. Aynı QR birden çok öğretmene okutulabilir (o sınıfa giren herkes).
6. Sihirbaz kapanır, tahta kilitlenir. Artık tek açma yolu telefondur.

Sonradan yeni öğretmen eklemek: Admin aracı → kurulum PIN'i → QR yeniden
gösterilir.

---

## 8. İlk sürüm kapsamı (MVP)

Yapılacaklar:
- [ ] Ortak kripto kütüphanesi (challenge/response üretimi ve doğrulaması)
- [ ] `TahtaKilit.Lock` — kilit ekranı, QR gösterimi, kod girişi, klavye kancası
- [ ] `TahtaKilit.Service` — açılışta kilitleme, watchdog, oturum takibi
- [ ] `TahtaKilit.Admin` — kurulum sihirbazı, eşleştirme QR'ı, takvim, loglar
- [ ] PWA — eşleştirme, QR okuma, elle kod, cevap üretimi, çevrimdışı çalışma
- [ ] MSI/EXE kurulum paketi
- [ ] Windows 10 tahta üzerinde saha testi

İlk sürüm dışında bırakılanlar (ileride):
- Bulut panel, merkezi log, uzaktan kilitleme
- Sessiz toplu kurulum parametreleri
- Shell replacement ile sertleştirme
- iOS/Android yerel uygulama

---

## 9. Bilinen sınırlar

| Sınır | Etki | Azaltma |
|---|---|---|
| Güvenli Mod ile atlatılabilir | Teknik bilen kullanıcı kilidi aşar | Gerekirse 2. aşamada politika ile Güvenli Mod kapatılır |
| `Ctrl+Alt+Del` engellenemez | Windows kilit ekranına düşülebilir | Görev Yöneticisi politika ile kapatılır, servis kilidi geri getirir |
| Anahtar diskte, yönetici okuyabilir | Yerel yönetici anahtarı çıkarabilir | DPAPI + ACL; tahtada öğrenciye yönetici hesabı verilmemeli |
| Telefon kaybolursa | Kaybolan telefon tahtaları açabilir | Uygulama PIN'i; tahtada anahtar yenileme (kurulum PIN'i ile) |
| Kamera izni verilmemişse | QR okunamaz | 6 haneli çağrı kodunu elle girme yolu her zaman açık |
