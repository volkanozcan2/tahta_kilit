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
| Protokol çekirdeği | `src/TahtaKilit.Core` | ✅ Hazır, testli |
| Telefon uygulaması (PWA) | `pwa/` | ✅ Çalışıyor, testli |
| Windows tutkalı | `src/TahtaKilit.Windows` | ✅ Yazıldı, derleniyor |
| Windows servisi | `src/TahtaKilit.Service` | ✅ Yazıldı, derleniyor |
| Kilit ekranı (WPF) | `src/TahtaKilit.Lock` | ✅ Yazıldı, derleniyor |
| Kurulum sihirbazı (WPF) | `src/TahtaKilit.Admin` | ✅ Yazıldı, derleniyor |

**Henüz gerçek bir Windows makinede çalıştırılmadı.** Derleniyor ve mantığı
test ediliyor, ama kilit ekranı, servis ve P/Invoke çağrıları sahada
denenmedi.

## Telefon uygulaması

**https://tahta-kilit.netlify.app**

Telefonda aç, tarayıcı menüsünden "Ana ekrana ekle" de. Kurulduktan sonra
internet gerekmez. Sayfa herkese açıktır ama tek başına bir işe yaramaz:
bir tahtayı açabilmek için o tahtanın eşleştirme karekodunu okutmuş olman
gerekir.

Yayınlamak için: `netlify.toml` yalnızca `pwa/` klasörünü yayınlar.

## Tahtaya kurmak

> **Önce sanal makinede dene.** Kurulumdan sonra tahta yalnızca eşleşmiş bir
> telefonla açılır. Telefon eşleştirmeden kurarsan makinede kilitli kalırsın —
> çıkış yolu aşağıdaki "Kilitli kaldıysan" bölümünde.

Windows'ta, yönetici PowerShell'de:

```powershell
powershell -ExecutionPolicy Bypass -File tools\yayinla.ps1   # derle ve topla
powershell -ExecutionPolicy Bypass -File tools\kur.ps1       # kur ve sihirbazı aç
```

Kurmadan denemek için: `tools\sihirbaz.ps1` (kurulum sihirbazı) ve
`tools\dene.ps1` (kilit ekranı). Windows 11'in Akıllı Uygulama Denetimi
imzasız programları engelliyorsa `tools\sandbox-ac.ps1` ile Korumalı Alan'da
dene — orada bu kısıtlama yok ve pencere kapanınca iz kalmaz.

Sihirbaz tahtaya bir ad ve kurulum PIN'i sorar, ardından eşleştirme karekodunu
gösterir. **Kurulumu bitirmeden önce telefonunla o karekodu okut** — tahtayı
açabilecek tek şey o.

Kaldırmak için: `powershell -ExecutionPolicy Bypass -File tools\kaldir.ps1`

### Kilitli kaldıysan

Servis Güvenli Mod'da başlamaz. Windows'u Güvenli Mod'da açıp
`tools\kaldir.ps1` çalıştırarak kilidi kaldırabilirsin. Bu aynı zamanda
sistemin bilinen sınırıdır: Güvenli Mod'a girebilen biri kilidi aşabilir
(bkz. [docs/TASARIM.md](docs/TASARIM.md)).

## Geliştirme

Proje **macOS ve Linux'ta da derlenir.** WPF projeleri dahil her şey derlenip
testler koşulabilir (`Directory.Build.props` içindeki `EnableWindowsTargeting`
sayesinde); yalnızca *çalıştırmak* Windows gerektirir.

Gerekenler: [.NET 8 SDK](https://dotnet.microsoft.com/download) ve Node.js 20+.

```bash
# Tüm projeler ve testler (macOS/Linux'ta da çalışır)
dotnet build
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
