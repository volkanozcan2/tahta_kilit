# Tahta Kilit

Windows 10 akıllı tahtaları kilitler; yalnızca öğretmenin telefonundaki
uygulamadan üretilen tek kullanımlık şifreyle açılır.

Tahtanın saati kaysa da, internet kopuk olsa da çalışır: zaman tabanlı kod
(TOTP) yerine **çağrı–cevap** yöntemi kullanılır. Tahta ekranda bir karekod ve
6 haneli çağrı kodu gösterir; öğretmen telefonuyla okutur, telefon 8 haneli
cevabı üretir, öğretmen tahtaya girer.

- Sunucu yok, internet yok, merkezi kayıt yok.
- Her açılışta kod değişir; görülen kod ikinci kez işe yaramaz.
- Bir telefon birden çok tahtayı açabilir.

Kararların tamamı ve mimari: **[docs/TASARIM.md](docs/TASARIM.md)**

## Durum

| Parça | Klasör | Durum |
|---|---|---|
| Protokol çekirdeği (C#) | `src/TahtaKilit.Core` | ✅ Hazır, testli |
| Telefon uygulaması (PWA) | `pwa/` | ✅ Çalışıyor (eşleştirme, karekod okuma, elle kod, şifre üretimi, çevrimdışı) |
| Kilit ekranı (WPF) | `src/TahtaKilit.Lock` | ⏳ Yapılacak |
| Windows servisi | `src/TahtaKilit.Service` | ⏳ Yapılacak |
| Kurulum sihirbazı | `src/TahtaKilit.Admin` | ⏳ Yapılacak |

## Geliştirme

`Core` ve PWA **platformdan bağımsızdır** — macOS ve Linux'ta da derlenip
test edilir. Yalnızca `Lock`, `Service` ve `Admin` Windows gerektirir.

Gerekenler: [.NET 8 SDK](https://dotnet.microsoft.com/download) ve Node.js 20+.

```bash
# C# çekirdeği ve testleri
dotnet test

# Telefon uygulaması testleri
npm install
npx playwright install chromium   # tarayıcı testleri için bir kez
npm test

# Telefon uygulamasını tarayıcıda aç
npm run serve                     # http://localhost:8080
```

> Kamera erişimi ve uygulamanın telefona kurulabilmesi için güvenli bağlam
> (HTTPS) gerekir. `localhost` güvenli sayıldığı için geliştirme sırasında
> sorun çıkmaz; telefonda denemek için HTTPS ile yayınlamak gerekir.

### Testler ne doğruluyor?

En kritik test, iki ayrı uygulamanın (C# ve JavaScript) **aynı şifreyi**
ürettiğini doğrular. İkisi ayrışırsa öğretmen tahtayı açamaz. Ortak test
vektörleri `spec/vectors.json` dosyasındadır ve her iki tarafta da koşulur.

Protokol değişirse vektörler yeniden üretilmelidir:

```bash
npm run vectors
```

## Protokol özeti

```
cevap = HMAC-SHA256(anahtar, "TK1:<tahta-kimliği>:<çağrı>")  →  8 hane
```

- **Anahtar** — tahta başına 256 bit, kurulumda üretilir, eşleştirme karekoduyla telefona aktarılır.
- **Çağrı** — 6 karakter (Crockford Base32), her denemede yeniden üretilir.
- **Cevap** — RFC 4226 dinamik kesme ile 8 haneye indirilir.
- Zaman ve ağ hiçbir adımda kullanılmaz.
