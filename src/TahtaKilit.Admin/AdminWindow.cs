using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TahtaKilit.Core;
using TahtaKilit.Windows;

namespace TahtaKilit.Admin;

/// <summary>
/// Kurulum sihirbazi ve yonetim ekrani.
///
/// Ilk calistirmada tahta adi ve kurulum PIN'i sorulur, gizli anahtar
/// uretilir, veri klasorunun izinleri kisitlanir ve eslestirme karekodu
/// gosterilir. Sonraki calistirmalarda PIN sorulur.
/// </summary>
internal sealed class AdminWindow : Window
{
    private readonly ConfigStore _store = new(WindowsPaths.ConfigFile, new DpapiProtector());

    private BoardConfig? _config;

    public AdminWindow()
    {
        Title = "Tahta Kilit — Kurulum";
        Width = 720;
        Height = 760;
        Background = Theme.Zemin;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _config = _store.Load();
        Content = _config is null ? BuildSetupPage() : BuildPinPage();
    }

    private static ScrollViewer Page(params UIElement[] children)
    {
        var panel = new StackPanel { Margin = new Thickness(36, 28, 36, 28) };
        foreach (var child in children)
            panel.Children.Add(child);

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    // ------------------------------------------------------------- 1. kurulum

    private UIElement BuildSetupPage()
    {
        var ad = Theme.Giris();
        var pin1 = Theme.Sifre();
        var pin2 = Theme.Sifre();
        var hata = Theme.Metin("", Theme.Hata);
        var kur = Theme.Dugme("Kur ve eşleştirme karekodunu göster", birincil: true);

        kur.Click += (_, _) =>
        {
            hata.Text = "";

            var tahtaAdi = ad.Text.Trim();
            if (tahtaAdi.Length == 0)
            {
                hata.Text = "Tahtaya bir ad ver, örneğin \"Z-Blok 204\".";
                return;
            }

            if (pin1.Password.Length < AdminPin.MinLength)
            {
                hata.Text = $"Kurulum PIN'i en az {AdminPin.MinLength} karakter olmalı.";
                return;
            }

            if (pin1.Password != pin2.Password)
            {
                hata.Text = "İki PIN aynı değil.";
                return;
            }

            try
            {
                _config = Kur(tahtaAdi, pin1.Password);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                hata.Text = "Yapılandırma yazılamadı: " + e.Message;
                return;
            }

            Content = BuildPairingPage(ilkKurulum: true);
        };

        return Page(
            Theme.Baslik("Tahta Kilit kurulumu"),
            Theme.Metin(
                "Bu tahta için bir gizli anahtar üretilecek. Anahtar yalnızca bu bilgisayarda " +
                "ve öğretmenlerin telefonunda bulunur; hiçbir sunucuya gönderilmez."),
            Theme.Etiket("Tahtanın adı (öğretmenin telefonunda görünecek)"),
            ad,
            Theme.Etiket($"Kurulum PIN'i (en az {AdminPin.MinLength} karakter)"),
            pin1,
            Theme.Etiket("Kurulum PIN'i (tekrar)"),
            pin2,
            Theme.Metin(
                "Bu PIN yeni öğretmen telefonu eklemek, takvimi değiştirmek ve kilidi kaldırmak " +
                "için gerekir. Öğrencilerle paylaşma.",
                Theme.Soluk),
            hata,
            kur);
    }

    private BoardConfig Kur(string tahtaAdi, string pin)
    {
        var config = new BoardConfig
        {
            BoardId = UnlockProtocol.NewBoardId(),
            BoardName = tahtaAdi,
            Key = Base64Url.Encode(UnlockProtocol.NewKey()),
            IdleLockMinutes = 0,
        };

        AdminPin.Set(config, pin);

        // Anahtarin asil korumasi klasor izinleridir; yapilandirma yazilmadan
        // once uygulanmalidir ki dosya hicbir an herkese acik olmasin.
        DataDirectorySecurity.Restrict(WindowsPaths.DataDirectory);
        _store.Save(config);

        return config;
    }

    // ------------------------------------------------------------- 2. PIN

    private UIElement BuildPinPage()
    {
        var pin = Theme.Sifre();
        var hata = Theme.Metin("", Theme.Hata);
        var gir = Theme.Dugme("Devam", birincil: true);

        void Dogrula()
        {
            if (AdminPin.Verify(_config!, pin.Password))
                Content = BuildMainPage();
            else
                hata.Text = "PIN yanlış.";
        }

        gir.Click += (_, _) => Dogrula();
        pin.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                Dogrula();
        };

        return Page(
            Theme.Baslik(_config!.BoardName),
            Theme.Metin("Devam etmek için kurulum PIN'ini gir."),
            Theme.Etiket("Kurulum PIN'i"),
            pin,
            hata,
            gir);
    }

    // ------------------------------------------------------------- 3. yonetim

    private UIElement BuildMainPage()
    {
        var durum = Theme.Metin("", Theme.Basari);

        var esle = Theme.Dugme("Öğretmen telefonu ekle (eşleştirme karekodu)", birincil: true);
        esle.Click += (_, _) => Content = BuildPairingPage(ilkKurulum: false);

        // --- Takvim ---
        var takvimAcik = Theme.Onay("Ders saati dışında tahtayı kilitle");
        var bas = Theme.Giris(5);
        var bit = Theme.Giris(5);
        var gunler = new[]
        {
            (Gun: DayOfWeek.Monday, Kutu: Theme.Onay("Pzt")),
            (Gun: DayOfWeek.Tuesday, Kutu: Theme.Onay("Sal")),
            (Gun: DayOfWeek.Wednesday, Kutu: Theme.Onay("Çar")),
            (Gun: DayOfWeek.Thursday, Kutu: Theme.Onay("Per")),
            (Gun: DayOfWeek.Friday, Kutu: Theme.Onay("Cum")),
            (Gun: DayOfWeek.Saturday, Kutu: Theme.Onay("Cmt")),
            (Gun: DayOfWeek.Sunday, Kutu: Theme.Onay("Paz")),
        };

        var mevcut = _config!.ToSchedule();
        if (mevcut.Windows.Count > 0)
        {
            var pencere = mevcut.Windows[0];
            takvimAcik.IsChecked = true;
            bas.Text = pencere.Start.ToString("HH\\:mm");
            bit.Text = pencere.End.ToString("HH\\:mm");

            foreach (var (gun, kutu) in gunler)
                kutu.IsChecked = pencere.Days.Contains(gun);
        }
        else
        {
            bas.Text = "07:30";
            bit.Text = "17:00";

            foreach (var (gun, kutu) in gunler)
                kutu.IsChecked = gun is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
        }

        var gunSatiri = new WrapPanel();
        foreach (var (_, kutu) in gunler)
            gunSatiri.Children.Add(kutu);

        var saatSatiri = new StackPanel { Orientation = Orientation.Horizontal };
        bas.Width = 90;
        bit.Width = 90;
        saatSatiri.Children.Add(Theme.Metin("Kullanılabilir saatler: ", Theme.Soluk));
        saatSatiri.Children.Add(bas);
        saatSatiri.Children.Add(Theme.Metin("  —  ", Theme.Soluk));
        saatSatiri.Children.Add(bit);

        // --- Bosta kalma ---
        var bosta = Theme.Giris(3);
        bosta.Width = 90;
        bosta.Text = _config.IdleLockMinutes.ToString();

        var kaydet = Theme.Dugme("Ayarları kaydet", birincil: true);
        kaydet.Click += (_, _) =>
        {
            durum.Foreground = Theme.Hata;

            if (!int.TryParse(bosta.Text.Trim(), out var dakika) || dakika is < 0 or > 240)
            {
                durum.Text = "Boşta kalma süresi 0 ile 240 dakika arasında olmalı (0 = kapalı).";
                return;
            }

            if (takvimAcik.IsChecked == true)
            {
                if (!TimeOnly.TryParseExact(bas.Text.Trim(), "HH\\:mm", out var baslangic) ||
                    !TimeOnly.TryParseExact(bit.Text.Trim(), "HH\\:mm", out var bitis))
                {
                    durum.Text = "Saatleri 07:30 biçiminde yaz.";
                    return;
                }

                var secilenGunler = gunler.Where(g => g.Kutu.IsChecked == true).Select(g => g.Gun).ToArray();
                if (secilenGunler.Length == 0)
                {
                    durum.Text = "En az bir gün seç.";
                    return;
                }

                _config.SetSchedule(new LockSchedule([new AllowedWindow(secilenGunler, baslangic, bitis)]));
            }
            else
            {
                _config.Schedule.Clear();
            }

            _config.IdleLockMinutes = dakika;

            try
            {
                _store.Save(_config);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                durum.Text = "Kaydedilemedi: " + e.Message;
                return;
            }

            durum.Foreground = Theme.Basari;
            durum.Text = "Ayarlar kaydedildi. Servis birkaç saniye içinde uygular.";
        };

        var kayitlar = Theme.Dugme("Kayıtları aç");
        kayitlar.Click += (_, _) => KayitlariAc(durum);

        var kaldir = Theme.Dugme("Kilidi kaldır (tahtayı serbest bırak)");
        kaldir.Foreground = Theme.Hata;
        kaldir.Click += (_, _) => Kaldir(durum);

        return Page(
            Theme.Baslik(_config.BoardName),
            Theme.Metin($"Tahta kimliği: {_config.BoardId}", Theme.Soluk),
            IzinUyarisi(),

            Theme.Etiket("ÖĞRETMENLER"),
            esle,
            Theme.Metin(
                "Karekodu okutan her öğretmen bu tahtayı açabilir. Telefonu kaybolan bir " +
                "öğretmen için anahtarı yenilemek gerekirse kurulumu baştan yap.",
                Theme.Soluk),

            Theme.Etiket("DERS SAATİ TAKVİMİ"),
            takvimAcik,
            saatSatiri,
            gunSatiri,
            Theme.Metin(
                "Takvim tahtayı yalnızca kilitler, asla kendiliğinden açmaz. Tahtanın saati " +
                "kaymışsa takvim geçici olarak işletilmez.",
                Theme.Soluk),

            Theme.Etiket("BOŞTA KALINCA KİLİTLE (dakika, 0 = kapalı)"),
            bosta,

            kaydet,
            durum,

            Theme.Etiket("DİĞER"),
            kayitlar,
            kaldir);
    }

    /// <summary>
    /// Veri klasoru herkese acik kalmissa anahtar okunabilir demektir;
    /// bu sessizce gecilmemeli.
    /// </summary>
    private UIElement IzinUyarisi()
    {
        if (DataDirectorySecurity.IsRestricted(WindowsPaths.DataDirectory))
            return Theme.Metin("Veri klasörü korumalı.", Theme.Basari);

        var duzelt = Theme.Dugme("İzinleri düzelt");
        var uyari = Theme.Metin(
            "UYARI: Veri klasörü öğrenci hesapları tarafından okunabilir durumda. " +
            "Gizli anahtar bu durumda korunmaz.",
            Theme.Hata);

        duzelt.Click += (_, _) =>
        {
            try
            {
                DataDirectorySecurity.Restrict(WindowsPaths.DataDirectory);
                uyari.Foreground = Theme.Basari;
                uyari.Text = "Veri klasörü korumaya alındı.";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                uyari.Text = "İzinler düzeltilemedi: " + e.Message;
            }
        };

        var panel = new StackPanel();
        panel.Children.Add(uyari);
        panel.Children.Add(duzelt);
        return panel;
    }

    private static void KayitlariAc(TextBlock durum)
    {
        if (!File.Exists(WindowsPaths.LogFile))
        {
            durum.Foreground = Theme.Soluk;
            durum.Text = "Henüz kayıt yok.";
            return;
        }

        Process.Start(new ProcessStartInfo(WindowsPaths.LogFile) { UseShellExecute = true });
    }

    private void Kaldir(TextBlock durum)
    {
        var onay = MessageBox.Show(
            "Bu tahtadaki kilit kaldırılacak ve gizli anahtar silinecek. " +
            "Öğretmenlerin telefonundaki kayıt da geçersiz olur.\n\nDevam edilsin mi?",
            "Kilidi kaldır",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (onay != MessageBoxResult.Yes)
            return;

        durum.Foreground = Theme.Hata;

        if (ServiceControl.IsInstalled() && ServiceControl.Stop() is { } hata)
        {
            durum.Text = "Servis durdurulamadı: " + hata;
            return;
        }

        try
        {
            File.Delete(WindowsPaths.ConfigFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            durum.Text = "Yapılandırma silinemedi: " + e.Message;
            return;
        }

        MessageBox.Show(
            "Kilit kaldırıldı. Tahta artık şifresiz açılıyor.",
            "Tahta Kilit",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Close();
    }

    // ------------------------------------------------------- 4. eslestirme QR

    private UIElement BuildPairingPage(bool ilkKurulum)
    {
        var icerik = QrJson.Serialize(
            PairingPayload.Create(_config!.BoardId, _config.BoardName, _config.DecodeKey()));

        var resim = new Image
        {
            Width = 340,
            Height = 340,
            Source = ToImage(QrCode.CreatePng(icerik, pixelsPerModule: 8)),
        };

        var cerceve = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 10),
            Child = resim,
        };

        var bitti = Theme.Dugme(ilkKurulum ? "Kurulumu bitir" : "Geri", birincil: true);
        bitti.Click += (_, _) =>
        {
            if (!ilkKurulum)
            {
                Content = BuildMainPage();
                return;
            }

            if (ServiceControl.IsInstalled())
                ServiceControl.Start();

            Close();
        };

        return Page(
            Theme.Baslik("Öğretmen telefonunu eşleştir"),
            Theme.Metin(
                "Öğretmen, telefonundaki Tahta Kilit uygulamasında \"Yeni tahta ekle\" deyip " +
                "bu karekodu okutsun. Aynı karekodu bu sınıfa giren her öğretmen okutabilir."),
            cerceve,
            Theme.Metin(
                "DİKKAT: Bu karekod tahtanın gizli anahtarını taşır. Ekranda öğrenciler " +
                "varken gösterme, fotoğrafını çektirme.",
                Theme.Hata),
            bitti);
    }

    private static BitmapImage ToImage(byte[] png)
    {
        var image = new BitmapImage();

        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        return image;
    }
}
