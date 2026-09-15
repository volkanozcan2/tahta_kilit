using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TahtaKilit.Windows;

/// <summary>
/// Ogretmenin sinifi terk ederken tahtayi hemen kilitlemesi icin kisayol
/// (Ctrl+Alt+L). Pencere tutamaci gerektirir.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GlobalHotkey : IDisposable
{
    /// <summary>Kisayol tetiklendiginde gelen pencere iletisi.</summary>
    public const int WM_HOTKEY = 0x0312;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_L = 0x4C;

    private readonly IntPtr _hwnd;
    private readonly int _id;
    private bool _registered;

    public GlobalHotkey(IntPtr hwnd, int id = 1)
    {
        _hwnd = hwnd;
        _id = id;
        _registered = RegisterHotKey(hwnd, id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_L);
    }

    /// <summary>Kisayol kaydedilemediyse (baska program kapmissa) <c>false</c>.</summary>
    public bool IsRegistered => _registered;

    /// <summary>Gelen iletinin bu kisayol olup olmadigini soyler.</summary>
    public bool Matches(int message, IntPtr wParam) =>
        message == WM_HOTKEY && wParam.ToInt32() == _id;

    public void Dispose()
    {
        if (!_registered)
            return;

        UnregisterHotKey(_hwnd, _id);
        _registered = false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
