namespace TahtaKilit.Windows;

/// <summary>Tahtadaki dosya konumlari.</summary>
public static class WindowsPaths
{
    /// <summary>
    /// Yapilandirma ProgramData altinda tutulur. Kurulum sirasinda klasorun
    /// erisim listesi SYSTEM ve Administrators ile sinirlanir; ogrenci
    /// hesabinin okuma izni olmaz.
    /// </summary>
    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TahtaKilit");

    public static string ConfigFile => Path.Combine(DataDirectory, "tahta.dat");

    public static string LogFile => Path.Combine(DataDirectory, "kayit.log");

    /// <summary>Kilit ekrani ile servis arasindaki borunun adi.</summary>
    public const string PipeName = "TahtaKilit.Kilit";
}
