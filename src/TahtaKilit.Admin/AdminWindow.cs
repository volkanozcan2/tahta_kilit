using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TahtaKilit.Core;
using TahtaKilit.Windows;

namespace TahtaKilit.Admin;

/// <summary>
/// Kurulum sihirbazi ve yonetim ekrani.
///
/// Ilk calistirmada yalnizca tahtanin adi sorulur; gizli anahtar veya
/// eslestirme yoktur. Kilidi acan sayi, tahtanin ekraninda gosterilen
/// sayidan hesaplanir (bkz. docs/TASARIM.md).
/// </summary>
internal sealed class AdminWindow : Window
{
    private readonly ConfigStore _store = new(WindowsPaths.ConfigFile, new DpapiProtector());

    private BoardConfig? _config;

    public AdminWindow()
    {
        Title = "Tahta Kilit — Kurulum";
        Width = 720;
        Height = 700;
        Background = Theme.Zemin;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _config = _store.Load();
        Content = _config is null ? BuildSetupPage() : BuildMainPage();
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
        var hata = Theme.Metin("", Theme.Hata);
        var kur = Theme.Dugme("Kur", birincil: true);

        kur.Click += (_, _) =>
        {
            hata.Text = "";

            var tahtaAdi = ad.Text.Trim();
            if (tahtaAdi.Length == 0)
            {
                hata.Text = "Tahtaya bir ad ver, örneğin \"Z-Blok 204\".";
                return;
            }

            try
            {
                _config = Kur(tahtaAdi);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                hata.Text = "Yapılandırma yazılamadı: " + e.Message;
                return;
            }

            if (ServiceControl.IsInstalled())
                ServiceControl.Start();

            Content = BuildMainPage();
        };

        return Page(
            Theme.Baslik("Tahta Kilit kurulumu"),
            Theme.Metin(
                "Tahta kilitlendiğinde ekranda 12 haneli bir sayı ve karekod gösterilir. " +
                "Öğretmen telefonundaki uygulamayla karekodu okutur, uygulama açma şifresini verir."),
            Theme.Etiket("Tahtanın adı (kilit ekranında görünecek)"),
            ad,
            UyariKutusu(),
            hata,
            kur);
    }

    private BoardConfig Kur(string tahtaAdi)
    {
        var config = new BoardConfig
        {
            BoardName = tahtaAdi,
            IdleLockMinutes = 0,
        };

        DataDirectorySecurity.Restrict(WindowsPaths.DataDirectory);
        _store.Save(config);

        return config;
    }

    /// <summary>
    /// Kurulum yapan kisi bu sistemin ne yapip ne yapmadigini bilmeli:
    /// kilit, kuralı bilen birini durdurmaz.
    /// </summary>
    private static UIElement UyariKutusu() => new Border
    {
        Background = Theme.Kart,
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(16),
        Margin = new Thickness(0, 18, 0, 6),
        Child = Theme.Metin(
            "Bilmen gereken: bu kilit gizli anahtar kullanmaz. Açma şifresi, ekranda " +
            "gösterilen sayıdan hesaplanır ve kuralı bilen herkes aynı sonucu bulabilir. " +
            "Düşünmeden veya yanlışlıkla kullanımı engeller; kararlı birini engellemez.",
            Theme.Soluk),
    };

    // ------------------------------------------------------------- 2. yonetim

    private UIElement BuildMainPage()
    {
        var durum = Theme.Metin("", Theme.Basari);

        var ad = Theme.Giris();
        ad.Text = _config!.BoardName;

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

        var mevcut = _config.ToSchedule();
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

        bas.Width = 90;
        bit.Width = 90;
        var saatSatiri = new StackPanel { Orientation = Orientation.Horizontal };
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

            var yeniAd = ad.Text.Trim();
            if (yeniAd.Length == 0)
            {
                durum.Text = "Tahtanın adı boş olamaz.";
                return;
            }

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

            _config.BoardName = yeniAd;
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
            IzinUyarisi(),

            Theme.Etiket("TAHTANIN ADI"),
            ad,

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
    /// Veri klasoru herkese acik kalmissa ayarlar kurcalanabilir demektir.
    /// </summary>
    private static UIElement IzinUyarisi()
    {
        if (DataDirectorySecurity.IsRestricted(WindowsPaths.DataDirectory))
            return Theme.Metin("Veri klasörü korumalı.", Theme.Basari);

        var duzelt = Theme.Dugme("İzinleri düzelt");
        var uyari = Theme.Metin(
            "UYARI: Veri klasörü öğrenci hesapları tarafından değiştirilebilir durumda.",
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
            "Bu tahtadaki kilit kaldırılacak.\n\nDevam edilsin mi?",
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
}
