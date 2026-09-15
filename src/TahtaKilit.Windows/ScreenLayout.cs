using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TahtaKilit.Windows;

/// <summary>Bir ekranin aygit pikseli cinsinden sinirlari.</summary>
public readonly record struct ScreenBounds(int Left, int Top, int Width, int Height);

/// <summary>
/// Baglı ekranlari listeler ve pencereleri tam olarak bir ekrani kaplayacak
/// sekilde yerlestirir.
///
/// WPF'in olcek bagimsiz birimleri yerine dogrudan aygit pikseli kullanilir:
/// tahtalarda olceklendirme %100 olmayabilir ve kilit ekraninda bir piksellik
/// bosluk bile kacis yolu demektir.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ScreenLayout
{
    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZE = 0x01000000;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>Tum ekranlarin sinirlari. Hicbiri bulunamazsa liste bostur.</summary>
    public static IReadOnlyList<ScreenBounds> All()
    {
        var list = new List<ScreenBounds>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr _, ref RECT rect, IntPtr _) =>
        {
            list.Add(new ScreenBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top));
            return true;
        }, IntPtr.Zero);

        return list;
    }

    /// <summary>Pencereyi verilen ekrani tam kaplayacak sekilde en uste yerlestirir.</summary>
    public static void Fill(IntPtr hwnd, ScreenBounds bounds, bool activate = true) =>
        SetWindowPos(
            hwnd, HWND_TOPMOST,
            bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            SWP_SHOWWINDOW | (activate ? 0 : SWP_NOACTIVATE));

    /// <summary>
    /// Pencerenin buyutulmus olma bayragini temizler. Buyutulmus bir pencere
    /// gorev cubugunu ortmez; kilit ekraninin ekrani tamamen kaplamasi gerekir.
    /// </summary>
    public static void ClearMaximizedStyle(IntPtr hwnd)
    {
        var style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZE);
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
}
