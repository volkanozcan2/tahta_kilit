using System.Runtime.Versioning;
using Microsoft.Win32;

namespace TahtaKilit.Windows;

/// <summary>
/// Kilitliyken Gorev Yoneticisini kapatir, kilit acilinca eski haline dondurur.
///
/// Ctrl+Alt+Del cekirdek surucusu olmadan engellenemez; kullanici Windows'un
/// kendi ekranina dusebilir. Bu politika oradan Gorev Yoneticisine gecip kilit
/// ekranini kapatmayi engeller. Servis zaten kilit ekranini geri getirir.
///
/// Onceki deger kendi anahtarimiza yazilir. Boylece kilit ekrani duzgun
/// kapanamadan oldurulse bile (oturum kapatma, zorla sonlandirma) bir sonraki
/// calismada politika geri alinabilir; kullanici Gorev Yoneticisi kapali
/// kalmis bir makineyle bas basa kalmaz.
/// </summary>
[SupportedOSPlatform("windows")]
public static class TaskManagerPolicy
{
    private const string PolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string ValueName = "DisableTaskMgr";

    private const string StateKey = @"Software\TahtaKilit";
    private const string StateValue = "OncekiGorevYoneticisi";

    /// <summary>Politika uygulanmadan onceki degerin "hic yoktu" karsiligi.</summary>
    private const string Absent = "yok";

    /// <summary>
    /// Gorev Yoneticisini kapatir ve onceki durumu kalici olarak kaydeder.
    /// Zaten uygulanmissa onceki durum korunur (ustune yazilmaz).
    /// </summary>
    public static void Disable()
    {
        using var policy = Registry.CurrentUser.CreateSubKey(PolicyKey);
        if (policy is null)
            return;

        using (var state = Registry.CurrentUser.CreateSubKey(StateKey))
        {
            // Ikinci kez cagrilirsa kendi yazdigimiz 1 degerini "onceki durum"
            // diye kaydetmeyelim.
            if (state is not null && state.GetValue(StateValue) is null)
            {
                var previous = policy.GetValue(ValueName);
                state.SetValue(StateValue, previous?.ToString() ?? Absent, RegistryValueKind.String);
            }
        }

        policy.SetValue(ValueName, 1, RegistryValueKind.DWord);
    }

    /// <summary>
    /// Kaydedilmis onceki durumu geri yazar. Geri alinacak bir sey yoksa
    /// hicbir sey yapmaz. Kilit acilirken ve kilit ekrani her baslarken
    /// cagrilir; ikinci durum, onceki calismadan kalan politikayi temizler.
    /// </summary>
    /// <returns>Geri alinacak bir sey bulunduysa <c>true</c>.</returns>
    public static bool Restore()
    {
        using var state = Registry.CurrentUser.OpenSubKey(StateKey, writable: true);
        if (state?.GetValue(StateValue) is not string saved)
            return false;

        using (var policy = Registry.CurrentUser.OpenSubKey(PolicyKey, writable: true))
        {
            if (policy is not null)
            {
                if (saved == Absent)
                    policy.DeleteValue(ValueName, throwOnMissingValue: false);
                else if (int.TryParse(saved, out var previous))
                    policy.SetValue(ValueName, previous, RegistryValueKind.DWord);
            }
        }

        state.DeleteValue(StateValue, throwOnMissingValue: false);
        return true;
    }
}
