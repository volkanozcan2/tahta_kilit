# Tahta Kilit

Windows 10 akıllı tahtaları kilitler; yalnızca öğretmenin telefonundaki
uygulamadan üretilen tek kullanımlık şifreyle açılır.

Tahtanın saati kaysa da, internet kopuk olsa da çalışır: zaman tabanlı kod
(TOTP) yerine **çağrı–cevap** yöntemi kullanılır. Tahta ekranda bir QR ve
6 haneli çağrı kodu gösterir; öğretmen telefonuyla okutur, telefon 8 haneli
cevabı üretir, öğretmen tahtaya girer.

- Sunucu yok, internet yok, merkezi kayıt yok.
- Her açılışta kod değişir; görülen kod ikinci kez işe yaramaz.
- Bir telefon birden çok tahtayı açabilir.

## Durum

Tasarım aşaması. Kararların tamamı ve mimari: **[docs/TASARIM.md](docs/TASARIM.md)**

## Parçalar

| Klasör | Ne işe yarar |
|---|---|
| `src/TahtaKilit.Service` | Windows servisi: açılışta kilitler, kilit ekranını ayakta tutar |
| `src/TahtaKilit.Lock` | Tam ekran kilit ekranı (WPF) |
| `src/TahtaKilit.Admin` | Kurulum sihirbazı, eşleştirme QR'ı, takvim, loglar |
| `src/TahtaKilit.Core` | Ortak kripto: çağrı üretimi ve cevap doğrulaması |
| `pwa/` | Telefon uygulaması (kurulabilir web uygulaması, çevrimdışı çalışır) |
