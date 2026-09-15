using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TahtaKilit.Core;

namespace TahtaKilit.Lock;

/// <summary>
/// Kilit ekrani. Solda karekod ve 6 haneli cagri, sagda 8 haneli sifre girisi.
///
/// Arayuz XAML yerine C# ile kurulur; tek dosyada, derleyici denetiminde.
/// Tahta dokunmatik oldugu icin ekran tus takimi zorunludur, ancak klavye
/// girisi de kabul edilir.
/// </summary>
internal sealed class LockScreenWindow : Window
{
    private static readonly Brush Zemin = new SolidColorBrush(Color.FromRgb(0x0F, 0x14, 0x19));
    private static readonly Brush Kart = new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x29));
    private static readonly Brush Yazi = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF6));
    private static readonly Brush Soluk = new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xB4));
    private static readonly Brush Vurgu = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly Brush Hata = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

    private readonly Func<string, Task<LockStatus?>> _unlock;

    private readonly TextBlock _boardName = Metin("", 26, Yazi);
    private readonly TextBlock _challenge = Mono("······", 52, Yazi);
    private readonly TextBlock _entry = Mono("", 44, Yazi);
    private readonly TextBlock _message = Metin("", 18, Soluk);
    private readonly Image _qr = new() { Width = 320, Height = 320, Margin = new Thickness(0, 12, 0, 12) };
    private readonly List<Button> _keys = [];

    private string _entered = "";
    private string _shownChallenge = "";
    private bool _closeAllowed;
    private bool _submitting;

    public LockScreenWindow(Func<string, Task<LockStatus?>> unlock)
    {
        _unlock = unlock;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Zemin;
        Content = BuildLayout();

        Closing += (_, e) => ((CancelEventArgs)e).Cancel = !_closeAllowed;
        PreviewKeyDown += OnKeyDown;
    }

    /// <summary>Kilit acilirken kapatmaya izin verir.</summary>
    public void AllowClose() => _closeAllowed = true;

    public void FocusEntry() => Focus();

    /// <summary>Servise henuz baglanilamadiysa gosterilir.</summary>
    public void ShowServiceWaiting()
    {
        _boardName.Text = "Tahta Kilit";
        _challenge.Text = "······";
        _message.Foreground = Soluk;
        _message.Text = "Kilit servisi başlatılıyor, lütfen bekleyin…";
        SetKeysEnabled(false);
    }

    /// <summary>Servisten gelen duruma gore ekrani tazeler.</summary>
    public void Update(LockStatus status)
    {
        _boardName.Text = string.IsNullOrWhiteSpace(status.BoardName) ? "Tahta Kilit" : status.BoardName;

        if (status.Challenge != _shownChallenge)
        {
            _shownChallenge = status.Challenge;
            _challenge.Text = status.Challenge;
            _qr.Source = ToImage(QrCode.CreatePng(status.ChallengeQr, pixelsPerModule: 8));
            ClearEntry();
        }

        if (status.WaitSeconds > 0)
        {
            SetKeysEnabled(false);
            _message.Foreground = Hata;
            _message.Text = $"Çok fazla yanlış deneme. Kalan süre: {Sure(status.WaitSeconds)}";
            return;
        }

        SetKeysEnabled(true);

        if (status.Result == LockStatus.Yanlis)
        {
            _message.Foreground = Hata;
            _message.Text = status.AttemptsLeft > 0
                ? $"Şifre yanlış. Tahtadaki kod yenilendi, baştan okut. Kalan hak: {status.AttemptsLeft}"
                : "Şifre yanlış.";
        }
        else if (_entered.Length == 0)
        {
            _message.Foreground = Soluk;
            _message.Text = "Şifreyi telefonundaki uygulamadan al.";
        }
    }

    private static string Sure(int saniye) => $"{saniye / 60:00}:{saniye % 60:00}";

    // -------------------------------------------------------------- yerlesim

    private UIElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(40) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Baslik
        var header = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        header.Children.Add(_boardName);
        header.Children.Add(Metin("Bu tahta kilitli", 16, Soluk));
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Orta: solda karekod, sagda sifre girisi
        var content = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var sol = BuildQrPanel();
        Grid.SetColumn(sol, 0);
        content.Children.Add(sol);

        var sag = BuildEntryPanel();
        Grid.SetColumn(sag, 2);
        content.Children.Add(sag);

        Grid.SetRow(content, 1);
        root.Children.Add(content);

        // Alt bilgi
        var footer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        footer.Children.Add(_message);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private StackPanel BuildQrPanel()
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        panel.Children.Add(Metin("Telefonundaki uygulamayla okut", 18, Soluk));

        // Karekod beyaz zemin uzerinde durmali; koyu zeminde okunmaz.
        panel.Children.Add(new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _qr,
        });

        panel.Children.Add(Metin("Kamera çalışmıyorsa bu kodu uygulamaya yaz", 16, Soluk));
        _challenge.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_challenge);

        return panel;
    }

    private StackPanel BuildEntryPanel()
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        panel.Children.Add(Metin("Telefonun verdiği şifre", 18, Soluk));

        _entry.Margin = new Thickness(0, 4, 0, 0);
        panel.Children.Add(new Border
        {
            Background = Kart,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24, 14, 24, 14),
            Margin = new Thickness(0, 8, 0, 16),
            MinWidth = 360,
            Child = _entry,
        });

        panel.Children.Add(BuildKeypad());
        return panel;
    }

    private UniformGrid BuildKeypad()
    {
        var pad = new UniformGrid { Rows = 4, Columns = 3, Width = 360, HorizontalAlignment = HorizontalAlignment.Center };

        foreach (var rakam in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" })
            pad.Children.Add(MakeKey(rakam, () => Append(rakam[0])));

        pad.Children.Add(MakeKey("Sil", Backspace));
        pad.Children.Add(MakeKey("0", () => Append('0')));
        pad.Children.Add(MakeKey("Temizle", ClearEntry));

        return pad;
    }

    private Button MakeKey(string caption, Action action)
    {
        var button = new Button
        {
            Content = caption,
            FontSize = caption.Length > 1 ? 18 : 26,
            Height = 72,
            Margin = new Thickness(6),
            Background = Kart,
            Foreground = Yazi,
            BorderThickness = new Thickness(0),
            Focusable = false,
        };

        button.Click += (_, _) => action();
        _keys.Add(button);

        return button;
    }

    private void SetKeysEnabled(bool enabled)
    {
        foreach (var key in _keys)
            key.IsEnabled = enabled;
    }

    // ----------------------------------------------------------------- giris

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Ogretmen isterse klavye de kullanabilir.
        if (e.Key is >= Key.D0 and <= Key.D9)
        {
            Append((char)('0' + (e.Key - Key.D0)));
            e.Handled = true;
        }
        else if (e.Key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            Append((char)('0' + (e.Key - Key.NumPad0)));
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            Backspace();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearEntry();
            e.Handled = true;
        }
    }

    private void Append(char digit)
    {
        if (_submitting || _entered.Length >= UnlockProtocol.ResponseDigits)
            return;

        _entered += digit;
        RenderEntry();

        // Sekiz hane tamamlaninca kendiliginden gonderilir; tahtada ayri bir
        // "onayla" tusuna basmak dokunmatikte fazladan adim olurdu.
        if (_entered.Length == UnlockProtocol.ResponseDigits)
            _ = SubmitAsync();
    }

    private void Backspace()
    {
        if (_submitting || _entered.Length == 0)
            return;

        _entered = _entered[..^1];
        RenderEntry();
    }

    private void ClearEntry()
    {
        _entered = "";
        RenderEntry();
    }

    private void RenderEntry()
    {
        var gosterilen = _entered.PadRight(UnlockProtocol.ResponseDigits, '·');
        _entry.Text = string.Join(' ', Enumerable.Range(0, 4).Select(i => gosterilen.Substring(i * 2, 2)));
    }

    private async Task SubmitAsync()
    {
        _submitting = true;
        SetKeysEnabled(false);

        try
        {
            var status = await _unlock(_entered);

            ClearEntry();

            if (status is null)
            {
                _message.Foreground = Hata;
                _message.Text = "Kilit servisine ulaşılamadı. Birkaç saniye sonra tekrar dene.";
                return;
            }

            if (status.Result == LockStatus.Acildi)
            {
                _message.Foreground = Vurgu;
                _message.Text = "Açılıyor…";
                return;
            }

            Update(status);
        }
        finally
        {
            _submitting = false;
            SetKeysEnabled(true);
        }
    }

    // --------------------------------------------------------------- yardim

    private static TextBlock Metin(string text, double size, Brush brush) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = brush,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 4, 0, 4),
    };

    private static TextBlock Mono(string text, double size, Brush brush)
    {
        var block = Metin(text, size, brush);
        block.FontFamily = new FontFamily("Consolas, Courier New, monospace");
        block.FontWeight = FontWeights.Bold;
        return block;
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
