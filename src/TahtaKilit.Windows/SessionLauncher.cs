using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace TahtaKilit.Windows;

/// <summary>
/// SYSTEM olarak calisan servisin, oturum acmis kullanicinin masaustunde
/// program baslatmasini saglar.
///
/// Servis 0 numarali oturumda calisir ve orada pencere gosteremez; kilit
/// ekraninin kullanicinin oturumunda acilmasi gerekir.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SessionLauncher
{
    private const uint MAXIMUM_ALLOWED = 0x02000000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint INVALID_SESSION = 0xFFFFFFFF;

    /// <summary>Su anda konsolda oturum acmis kullanicinin oturum numarasi.</summary>
    public static uint? ActiveSessionId()
    {
        var id = WTSGetActiveConsoleSessionId();
        return id == INVALID_SESSION ? null : id;
    }

    /// <summary>
    /// Verilen programi etkin oturumda baslatir ve surec kimligini dondurur.
    /// Oturum acik degilse <c>null</c> doner (kullanici henuz giris yapmamis).
    /// </summary>
    /// <exception cref="Win32Exception">Baslatma basarisiz olursa.</exception>
    public static int? LaunchInActiveSession(string executablePath, string arguments = "")
    {
        if (ActiveSessionId() is not { } sessionId)
            return null;

        if (!WTSQueryUserToken(sessionId, out var userToken))
            return null; // oturum var ama kullanici giris yapmamis

        var environment = IntPtr.Zero;
        var duplicated = IntPtr.Zero;

        try
        {
            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
                    TOKEN_TYPE.TokenPrimary, out duplicated))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Kullanici belirteci kopyalanamadi.");
            }

            if (!CreateEnvironmentBlock(out environment, duplicated, false))
                environment = IntPtr.Zero; // ortam olmadan da devam edilebilir

            var startup = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFO>(),
                lpDesktop = @"winsta0\default",
            };

            var commandLine = new StringBuilder($"\"{executablePath}\" {arguments}".TrimEnd());

            if (!CreateProcessAsUser(
                    duplicated, null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                    CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW,
                    environment, null, ref startup, out var info))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Kilit ekrani baslatilamadi.");
            }

            CloseHandle(info.hThread);
            CloseHandle(info.hProcess);

            return (int)info.dwProcessId;
        }
        finally
        {
            if (environment != IntPtr.Zero) DestroyEnvironmentBlock(environment);
            if (duplicated != IntPtr.Zero) CloseHandle(duplicated);
            CloseHandle(userToken);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken, uint desiredAccess, IntPtr tokenAttributes,
        SECURITY_IMPERSONATION_LEVEL impersonationLevel, TOKEN_TYPE tokenType, out IntPtr newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessAsUser(
        IntPtr token, string? applicationName, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles,
        uint creationFlags, IntPtr environment, string? currentDirectory,
        ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private enum SECURITY_IMPERSONATION_LEVEL { SecurityAnonymous, SecurityIdentification, SecurityImpersonation, SecurityDelegation }

    private enum TOKEN_TYPE { TokenPrimary = 1, TokenImpersonation }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize;
        public int dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public uint dwProcessId, dwThreadId;
    }
}
