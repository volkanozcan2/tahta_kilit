using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace TahtaKilit.Lock;

/// <summary>
/// Ikincil ekranlari ortan bos pencere. Kilitliyken hicbir icerik gorunmemeli.
/// </summary>
internal sealed class BlankWindow : Window
{
    private bool _closeAllowed;

    public BlankWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.Black;
        Cursor = System.Windows.Input.Cursors.None;

        Closing += OnClosing;
    }

    /// <summary>Kilit acilirken kapatmaya izin verir.</summary>
    public void AllowClose() => _closeAllowed = true;

    private void OnClosing(object? sender, CancelEventArgs e) => e.Cancel = !_closeAllowed;
}
