using System.Runtime.Versioning;
using System.Security.Cryptography;
using TahtaKilit.Core;

namespace TahtaKilit.Windows;

/// <summary>
/// Yapilandirmayi Windows'un kendi koruma servisiyle (DPAPI) sifreler.
/// Makine kapsami kullanilir: dosya baska bir bilgisayara kopyalanirsa
/// cozulemez.
///
/// Durust sinir: tahtada yonetici yetkisi olan biri ayni makinede calisip
/// veriyi cozebilir. Koruma, yetkisiz <em>kullanima</em> karsidir; makineye
/// yonetici olarak erisen birine karsi degil.
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
