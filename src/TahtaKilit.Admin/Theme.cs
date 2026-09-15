using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TahtaKilit.Admin;

/// <summary>Yonetim ekraninin ortak gorunum parcalari.</summary>
internal static class Theme
{
    public static readonly Brush Zemin = new SolidColorBrush(Color.FromRgb(0x0F, 0x14, 0x19));
    public static readonly Brush Kart = new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x29));
    public static readonly Brush Yazi = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF6));
    public static readonly Brush Soluk = new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xB4));
    public static readonly Brush Vurgu = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
    public static readonly Brush Hata = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
    public static readonly Brush Basari = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));

    public static TextBlock Baslik(string text) => new()
    {
        Text = text,
        FontSize = 24,
        FontWeight = FontWeights.SemiBold,
        Foreground = Yazi,
        Margin = new Thickness(0, 0, 0, 6),
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Metin(string text, Brush? brush = null, double size = 14) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = brush ?? Soluk,
        Margin = new Thickness(0, 4, 0, 4),
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Etiket(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = Soluk,
        Margin = new Thickness(0, 14, 0, 4),
    };

    public static TextBox Giris(int maxLength = 60) => new()
    {
        FontSize = 16,
        Padding = new Thickness(10, 8, 10, 8),
        Background = Kart,
        Foreground = Yazi,
        BorderThickness = new Thickness(0),
        MaxLength = maxLength,
    };

    public static PasswordBox Sifre() => new()
    {
        FontSize = 16,
        Padding = new Thickness(10, 8, 10, 8),
        Background = Kart,
        Foreground = Yazi,
        BorderThickness = new Thickness(0),
        MaxLength = 32,
    };

    public static Button Dugme(string caption, bool birincil = false)
    {
        return new Button
        {
            Content = caption,
            FontSize = 15,
            FontWeight = birincil ? FontWeights.SemiBold : FontWeights.Normal,
            Padding = new Thickness(18, 10, 18, 10),
            Margin = new Thickness(0, 10, 0, 0),
            Background = birincil ? Vurgu : Kart,
            Foreground = birincil ? Brushes.White : Yazi,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
    }

    public static CheckBox Onay(string caption) => new()
    {
        Content = caption,
        Foreground = Yazi,
        FontSize = 14,
        Margin = new Thickness(0, 6, 12, 6),
    };
}
