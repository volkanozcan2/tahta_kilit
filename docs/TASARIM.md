# Tahta Kilit — Tasarım Kararları

Windows 10/11 akıllı tahtaları kilitleyen, telefondaki uygulamadan üretilen
şifreyle açılan sistem.

---

## 1. Özet

| Konu | Karar |
|---|---|
| Kilit yöntemi | Kiosk overlay (tam ekran, her zaman üstte) + Windows servisi watchdog |
| Açma kuralı | Ekrandaki 12 haneli sayının iki yarısının XOR'u |
| Gizli anahtar | **Yok** (bilinçli tercih — bkz. 2. bölüm) |
| Kod ömrü | 30 saniye |
| Sunucu | Yok. Her şey tahtada ve telefonda yerel |
| Mobil taraf | PWA (telefona kurulabilen web uygulaması), çevrimdışı çalışır |
| Kurulum | Yalnızca tahtanın adı sorulur |
| Kilitlenme anı | Her açılışta + ders saati takvimine göre + boşta kalınca |

---

## 2. Güvenlik modeli — önce bu okunmalı

**Bu sistem gizli anahtar kullanmaz.** Kilidi açan sayı, kilidin kendi
ekranında gösterilen sayıdan hesaplanır. Kuralı bilen herkes — bir hesap
makinesiyle, ya da bu depodaki telefon uygulamasını açarak — aynı sonucu
bulabilir.

Yani bu bir güvenlik önlemi **değildir.** Şunu yapar:

- Tahtayı düşünmeden veya yanlışlıkla kullanmayı engeller
- "Bu tahta kilitli, kullanman beklenmiyor" mesajını açıkça verir
- Öğretmene tek dokunuşla açma yolu bırakır

Şunu **yapmaz:**

- Kuralı öğrenen bir öğrenciyi durdurmaz
- Telefon uygulamasını bulan birini durdurmaz (uygulama herkese açık bir
  adreste yayınlanır ve içinde tahtaya özel hiçbir bilgi yoktur)

Bu, projeyi yürüten kişinin bilinçli tercihidir: kurulumun basit olması
(tahta başına eşleştirme yok, öğretmen telefonunda hesap yok) gerçek bir
kilide tercih edilmiştir.

### Gerçek koruma gerekirse

Önceki sürümde HMAC-SHA256 tabanlı, tahta başına gizli anahtarlı bir çağrı–cevap
şeması vardı; öğretmen telefonu kurulumda karekodla eşleşiyordu. O şema
kuralı bilen birine karşı da koruma sağlıyordu ve git geçmişinde duruyor
(`src/TahtaKilit.Core/UnlockProtocol.cs`). Kurulum kolaylığından ödün vermeden
güvenlik isteniyorsa ara yol, okul geneli tek anahtardır: tahta kurulumu yine
"isim gir, geç" olur, öğretmen telefonu ise hayatında bir kez eşleşir.

---

## 3. Açma kuralı

```
   TAHTA (kilitli)                         TELEFON
   ──────────────                          ───────
1. 12 haneli rastgele sayı üretir
   (30 saniyede bir yenilenir)
   Ekranda gösterir:
     ┌─────────────┐
     │   [ QR ]    │  ─── QR okut ────►  2. Sayıyı okur: 123456 654321
     │             │      (veya)            (kamera yoksa elle yazılır)
     │ 1234 5665   │
     │ 4321        │                     3. İki yarıyı XOR'lar:
     └─────────────┘                        123456 ^ 654321 = 530865
                                            → 0530865
5. Aynı hesabı kendi de yapar,     ◄──── 4. Ekranda: 053 0865
   eşleşiyorsa kilidi açar                    (öğretmen tahtaya yazar)
```

**Ayrıntılar**

- Sayı 12 hane, başta sıfır olabilir.
- İki yarı 6 haneli tam sayı olarak okunur, bit düzeyinde XOR'lanır.
- Sonuç en fazla 1048575 (2²⁰−1) olabilir, yani 7 hane. Kısa sonuçlar başa
  sıfır konarak 7 haneye tamamlanır; ekranda hep aynı genişlikte görünür.
- Kod 30 saniyede bir yenilenir. Ölçüm sistem saatiyle değil, açılıştan beri
  geçen süreyle yapılır — tahtanın saati kaysa da çalışır.
- Kilit açıldığında kod hemen yenilenir; tahta tekrar kilitlenince eski şifre
  işe yaramaz.
- Yanlış şifre girilirse kod **değişmez**; öğretmen yazım hatasını düzeltip
  tekrar deneyebilir, karekodu yeniden okutması gerekmez.

---

## 4. Windows tarafı mimarisi

Üç parça, .NET 8:

**`TahtaKilit.Service`** — Windows servisi, SYSTEM olarak çalışır
- Açılışta kilit durumunu belirler (varsayılan: kilitli).
- Kilit ekranını aktif oturuma başlatır (`CreateProcessAsUser`).
- Watchdog: kilit ekranı öldürülürse 1 saniye içinde yeniden başlatır.
- Ders saati takvimini uygular, yapılandırma değişince yeniden okur.

**`TahtaKilit.Lock`** — WPF tam ekran kilit ekranı, kullanıcı oturumunda
- Tüm ekranları kaplar (çoklu monitör), görev çubuğu gizlenir.
- Karekod + 12 haneli sayı + 7 haneli şifre girişi (dokunmatik tuş takımı).
- Düşük seviye klavye kancası ile `Win`, `Alt+Tab`, `Alt+F4`, `Ctrl+Esc`
  engellenir.
- `Ctrl+Alt+L` ile öğretmen çıkarken elle kilitleyebilir.

**`TahtaKilit.Admin`** — kurulum sihirbazı ve yönetim aracı
- İlk çalıştırmada yalnızca tahtanın adını sorar.
- Ders saati takvimi, boşta kalma süresi, kayıtlar, kilidi kaldırma.

Kilit durumu servistedir; kilit ekranı yalnızca girilen yazıyı iletip sonucu
alır. Bu, kuralda sır olmadığı için güvenlik sağlamaz — ama kilit ekranı
öldürülüp yeniden başlatıldığında kilidin açılmamasını sağlar.

### `Ctrl+Alt+Del` hakkında

Secure Attention Sequence, çekirdek sürücüsü olmadan engellenemez. Alınan
önlemler:
- Kilitliyken Görev Yöneticisi politika ile kapatılır; kilit açılınca eski
  haline döndürülür. Önceki değer kendi kayıt anahtarımızda saklanır, böylece
  kilit ekranı zorla sonlandırılsa bile politika bir sonraki açılışta geri
  alınır.
- Kullanıcı Windows'un kendi kilit ekranına düşebilir; oturuma döndüğünde
  servis kilit ekranını hemen yeniden öne getirir.

---

## 5. Ders saati takvimi ve saat kayması

Takvim **yalnızca kilitleme yönünde** çalışır:

- Takvim tahtayı kilitleyebilir, **asla kendi başına açamaz.**
- Açmanın tek yolu her zaman ekrandaki koddan hesaplanan şifredir.

Saat şaşsa bile en kötü ihtimalle tahta beklenmedik anda kilitlenir —
öğretmen saniyeler içinde açar. Ters durumda (saat yüzünden tahtanın
kendiliğinden açık kalması) daha büyük sorun doğardı.

Servis en son gördüğü zamanı diske yazar; sistem saati bundan belirgin şekilde
geriye gitmişse takvim geçici olarak işletilmez.

**Kilitlenme tetikleri**
1. Her Windows açılışında (varsayılan, kapatılamaz).
2. Takvim dışı saatlerde — isteğe bağlı.
3. Boşta kalınca — isteğe bağlı.
4. `Ctrl+Alt+L` — öğretmen çıkarken.

---

## 6. Telefon tarafı (PWA)

- **Teknoloji:** saf HTML/CSS/JS. `jsQR` ile kamera okuma.
- **Çevrimdışı:** Service Worker tüm dosyaları önbelleğe alır.
- **Saklanan veri yok:** eşleştirme, hesap, anahtar, PIN yok. Uygulama
  karekodu okur, XOR'u hesaplar, sonucu gösterir.
- **Ekranlar:** ana ekran → kamera → sonuç. Kamera çalışmazsa 12 haneli sayı
  elle girilebilir.
- **Barındırma:** statik dosyalar, HTTPS şart (kamera erişimi ve PWA kurulumu
  için).

---

## 7. Kurulum akışı

1. BT sorumlusu tahtaya kurulum yapar (yönetici hakkı ister).
2. Sihirbaz tahtanın adını sorar. Başka hiçbir şey sormaz.
3. "Kur" denince tahta kilitlenir.
4. Öğretmen telefonunda PWA'yı açar, karekodu okutur, çıkan şifreyi girer.

Aynı telefon uygulaması bütün tahtalarda çalışır; tahtaya özel hiçbir kurulum
gerekmez.

---

## 8. Durum

- [x] Açma kuralı ve testleri
- [x] PWA — karekod okuma, elle giriş, çevrimdışı çalışma
- [x] `TahtaKilit.Lock` — kilit ekranı, klavye kancası
- [x] `TahtaKilit.Service` — açılışta kilitleme, watchdog, takvim
- [x] `TahtaKilit.Admin` — kurulum sihirbazı, ayarlar, kayıtlar
- [x] Kurulum betikleri
- [x] Windows 11'de uçtan uca deneme (önceki sürümle)
- [ ] Bu sürümün Windows'ta denenmesi
- [ ] Windows 10 akıllı tahta üzerinde saha testi

---

## 9. Bilinen sınırlar

| Sınır | Etki | Azaltma |
|---|---|---|
| **Gizli anahtar yok** | Kuralı bilen herkes kilidi açabilir | Yok — bilinçli tercih (bkz. 2. bölüm) |
| Güvenli Mod ile atlatılabilir | Teknik bilen kullanıcı kilidi aşar | Gerekirse politika ile Güvenli Mod kapatılır |
| `Ctrl+Alt+Del` engellenemez | Windows kilit ekranına düşülebilir | Görev Yöneticisi kapatılır, servis kilidi geri getirir |
| İmzasız çalıştırılabilir dosyalar | Windows 11 Akıllı Uygulama Denetimi engelliyor | Kod imzalama sertifikası, veya denetimin kapalı olduğu makineler |
| Kamera izni verilmemişse | Karekod okunamaz | 12 haneli sayıyı elle girme yolu her zaman açık |
