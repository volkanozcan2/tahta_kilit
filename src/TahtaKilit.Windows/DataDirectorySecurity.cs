using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace TahtaKilit.Windows;

/// <summary>
/// Veri klasorunu yalnizca SYSTEM ve yoneticilere acar.
///
/// Bu, gizli anahtarin asil korumasidir: DPAPI'nin makine kapsami ayni
/// makinedeki her kullanici tarafindan cozulebildigi icin, dosyanin ogrenci
/// hesabi tarafindan <em>okunamamasi</em> gerekir.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DataDirectorySecurity
{
    /// <summary>
    /// Klasoru olusturur ve erisimi kisitlar. Kurulum sirasinda yonetici
    /// yetkisiyle cagrilmalidir.
    /// </summary>
    public static void Restrict(string directory)
    {
        var info = Directory.CreateDirectory(directory);
        var security = new DirectorySecurity();

        // Devralinan izinler kapatilir; aksi halde "Users" grubu okuyabilir.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(sid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        info.SetAccessControl(security);
    }

    /// <summary>
    /// Klasorun gercekten kisitli olup olmadigini soyler. Kurulum sonrasi
    /// dogrulama ve Admin ekranindaki uyari icin kullanilir.
    /// </summary>
    public static bool IsRestricted(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        var rules = new DirectoryInfo(directory)
            .GetAccessControl()
            .GetAccessRules(true, true, typeof(SecurityIdentifier));

        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType != AccessControlType.Allow)
                continue;

            var sid = (SecurityIdentifier)rule.IdentityReference;

            if (sid.IsWellKnown(WellKnownSidType.BuiltinUsersSid) ||
                sid.IsWellKnown(WellKnownSidType.WorldSid) ||
                sid.IsWellKnown(WellKnownSidType.AuthenticatedUserSid))
            {
                return false;
            }
        }

        return true;
    }
}
