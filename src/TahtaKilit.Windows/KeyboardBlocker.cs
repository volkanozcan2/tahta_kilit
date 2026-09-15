using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TahtaKilit.Windows;

/// <summary>
/// Kilitliyken kacis tuslarini engeller: Win, Alt+Tab, Alt+F4, Ctrl+Esc,
/// Alt+Esc, Ctrl+Shift+Esc.
///
/// Ctrl+Alt+Del engellenemez (cekirdek surucusu gerekir); ona karsi
/// <see cref="TaskManagerPolicy"/> ve servisin watchdog'u devrededir.
///
/// Kanca, ileti dongusu olan bir is parcaciginda kurulmalidir (WPF'in ana
/// is parcacigi uygundur).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class KeyboardBlocker : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_F4 = 0x73;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt

    private readonly HookProc _proc;
    private IntPtr _hook;

    /// <summary>Kanca kurulamadiysa <c>false</c>; cagiran bunu kayda almalidir.</summary>
    public bool IsActive => _hook != IntPtr.Zero;

    public KeyboardBlocker()
    {
        _proc = HookCallback; // GC toplamasin diye alanda tutulur
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        var message = (int)wParam;
        if (message is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            var key = Marshal.ReadInt32(lParam);
            if (ShouldBlock(key))
                return 1; // tusu yut
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool ShouldBlock(int key)
    {
        var alt = IsDown(VK_MENU);
        var ctrl = IsDown(VK_CONTROL);
        var shift = IsDown(VK_SHIFT);

        return key switch
        {
            VK_LWIN or VK_RWIN => true,               // Baslat menusu
            VK_TAB when alt => true,                  // Alt+Tab
            VK_F4 when alt => true,                   // Alt+F4
            VK_ESCAPE when alt || ctrl => true,       // Alt+Esc, Ctrl+Esc, Ctrl+Shift+Esc
            _ => false,
        };

        static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
