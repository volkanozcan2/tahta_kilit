using System.Runtime.Versioning;
using Microsoft.Win32;

namespace TahtaKilit.Windows;

/// <summary>
/// Kilitliyken Gorev Yoneticisini kapatir, kilit acilinca eski haline dondurur.
///
/// Ctrl+Alt+Del cekirdek surucusu olmadan engellenemez; kullanici Windows'un
/// kendi ekranina dusebilir. Bu politika oradan Gorev Yoneticisine gecip kilit
/// ekranini kapatmayi engeller. Servis zaten kilit ekranini geri getirir.
/// </summary>
[SupportedOSPlatform("windows")]
public static class TaskManagerPolicy
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string ValueName = "DisableTaskMgr";

    /// <summary>
    /// Politikayi uygular ve onceki degeri dondurur. Bu deger
    /// <see cref="Restore"/> ile geri yazilmalidir.
    /// </summary>
    public static object? Disable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        var previous = key.GetValue(ValueName);
        key.SetValue(ValueName, 1, RegistryValueKind.DWord);
        return previous;
    }

    /// <summary>Onceki durumu geri yazar.</summary>
    public static void Restore(object? previous)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        if (key is null)
            return;

        if (previous is null)
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        else
            key.SetValue(ValueName, previous, RegistryValueKind.DWord);
    }
}
