using System.Runtime.Versioning;
using System.Security.Cryptography;
using TahtaKilit.Core;

namespace TahtaKilit.Windows;

/// <summary>
/// Yapilandirmayi Windows'un kendi koruma servisiyle (DPAPI) sifreler.
/// Makine kapsami kullanilir: dosya baska bir bilgisayara kopyalanirsa
/// cozulemez.
///
/// Durust sinir: makine kapsami, <em>ayni makinedeki</em> herhangi bir
/// kullanicinin cozebilecegi anlamina gelir. Asil bariyer dosya izinleridir
/// (bkz. <see cref="DataDirectorySecurity"/>): klasor yalnizca SYSTEM ve
/// Administrators tarafindan okunabilir. DPAPI, dosyanin baska makineye
/// tasinmasina ve yedeklerden okunmasina karsi ikinci katmandir.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiProtector : IDataProtector
{
    /// <summary>Sifrelemeye karistirilan sabit ek veri.</summary>
    private static readonly byte[] Entropy = "TahtaKilit/v1"u8.ToArray();

    public byte[] Protect(byte[] plain) =>
        ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine);

    public byte[]? Unprotect(byte[] protectedData)
    {
        try
        {
            return ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.LocalMachine);
        }
        catch (CryptographicException)
        {
            return null; // baska makine veya bozuk dosya
        }
    }
}
