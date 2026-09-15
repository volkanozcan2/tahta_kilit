using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TahtaKilit.Windows;

/// <summary>
/// Kullanicinin en son ne zaman klavye/fare kullandigini soyler.
///
/// Yalnizca kullanici oturumunda anlamlidir; SYSTEM olarak calisan servis
/// kendi oturumunu olcer. Bu yuzden bosta kalma denetimi kullanici
/// oturumundaki kilit ajaninda yapilir.
/// </summary>
[SupportedOSPlatform("windows")]
public static class IdleTime
{
    /// <summary>Son girdinin uzerinden gecen sure.</summary>
    public static TimeSpan Elapsed()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };

        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        // Ikisi de acilistan beri gecen milisaniye; tasma cikarma ile dogru calisir.
        var ms = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(ms);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
