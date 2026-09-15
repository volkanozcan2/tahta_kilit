# Tahta Kilit

Windows 10/11 akıllı tahtaları kilitler; telefondaki uygulamadan üretilen
şifreyle açılır.

Tahta kilitlendiğinde ekranda 12 haneli bir sayı ve karekodu gösterilir.
Öğretmen telefonuyla karekodu okutur; uygulama sayının iki yarısını XOR'layıp
7 haneli şifreyi verir, öğretmen tahtaya girer. Sayı 30 saniyede bir yenilenir.

- Sunucu yok, internet yok, eşleştirme yok, saklanan veri yok.
- Kurulumda yalnızca tahtanın adı sorulur.
- Aynı telefon uygulaması bütün tahtalarda çalışır.

> **Bu bir güvenlik önlemi değildir.** Gizli anahtar yoktur: kilidin cevabı,
> kilidin ekranında gösterilen sayıdan hesaplanır ve kuralı bilen herkes aynı
> sonucu bulabilir. Düşünmeden veya yanlışlıkla kullanımı engeller; kararlı
> birini engellemez. Bu bilinçli bir tercihtir —
> gerekçesi ve gerçek koruma isteyenler için alternatif:
> [docs/TASARIM.md](docs/TASARIM.md#2-güvenlik-modeli--önce-bu-okunmalı)

Kararların tamamı ve mimari: **[docs/TASARIM.md](docs/TASARIM.md)**

## Durum

| Parça | Klasör | Durum |
|---|---|---|
| Açma kuralı | `src/TahtaKilit.Core` | ✅ Hazır, testli |
| Telefon uygulaması (PWA) | `pwa/` | ✅ Çalışıyor, testli |
| Windows tutkalı | `src/TahtaKilit.Windows` | ✅ Yazıldı, derleniyor |
| Windows servisi | `src/TahtaKilit.Service` | ✅ Yazıldı, derleniyor |
| Kilit ekranı (WPF) | `src/TahtaKilit.Lock` | ✅ Yazıldı, derleniyor |
| Kurulum sihirbazı (WPF) | `src/TahtaKilit.Admin` | ✅ Çalışıyor |

Windows 11'de (Korumalı Alan içinde) uçtan uca denendi — ancak bu deneme
**önceki, gizli anahtarlı sürümle** yapıldı. XOR sürümü derleniyor ve testleri
geçiyor, henüz Windows'ta çalıştırılmadı.

**Henüz denenmemiş olanlar:** bu sürümün tamamı Windows'ta, açılışta
kilitlenme, servis watchdog'u, ders saati takvimi, boşta kalma kilidi, çoklu
monitör, kaçış tuşlarının gerçekten engellenmesi ve gerçek bir akıllı tahta.

## Telefon uygulaması

**https://tahta-kilit.netlify.app**

Telefonda aç, tarayıcı menüsünden "Ana ekrana ekle" de. Kurulduktan sonra
internet gerekmez. Eşleştirme, hesap veya saklanan veri yoktur: uygulama
karekodu okur, XOR'u hesaplar, şifreyi gösterir.

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

Sihirbaz yalnızca tahtanın adını sorar. "Kur" denince tahta kilitlenir;
telefonla karekodu okutup çıkan şifreyi girerek açarsın.

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

## Açma kuralı özeti

```
şifre = (ilk 6 hane) XOR (son 6 hane)  →  7 haneye tamamlanır

örnek:  1234 5665 4321  →  123456 XOR 654321 = 530865  →  053 0865
```

- **Sayı** — 12 hane, 30 saniyede bir yenilenir.
- **Şifre** — en fazla 1048575 olabildiği için başa sıfır konarak 7 haneye
  tamamlanır.
- Süre ölçümü sistem saatiyle değil açılıştan beri geçen süreyle yapılır;
  tahtanın saati kaysa da çalışır.
- Ağ hiçbir adımda kullanılmaz.
