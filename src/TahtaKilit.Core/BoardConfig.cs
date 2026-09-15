using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TahtaKilit.Core;

/// <summary>
/// Tahtanin kalici yapilandirmasi. Diske <em>sifreli</em> yazilir
/// (bkz. <see cref="ConfigStore"/>); bu sinif duz halini temsil eder.
/// </summary>
public sealed class BoardConfig
{
    public const int CurrentVersion = 1;

    [JsonPropertyName("v")] public int V { get; set; } = CurrentVersion;

    /// <summary>Tahta kimligi (karekodlarda gorunur).</summary>
    [JsonPropertyName("id")] public string BoardId { get; set; } = "";

    /// <summary>Ogretmenin gordugu ad, ornegin "Z-Blok 204".</summary>
    [JsonPropertyName("ad")] public string BoardName { get; set; } = "";

    /// <summary>Gizli anahtar (base64url). Telefonla paylasilan tek sir budur.</summary>
    [JsonPropertyName("k")] public string Key { get; set; } = "";

    /// <summary>Kurulum PIN'inin tuzu ve ozeti; PIN'in kendisi saklanmaz.</summary>
    [JsonPropertyName("pinTuz")] public string AdminPinSalt { get; set; } = "";
    [JsonPropertyName("pinOzet")] public string AdminPinHash { get; set; } = "";

    /// <summary>
    /// Yanlis deneme ceza kademesi. Diske yazilir ki tahtayi kapatip acmak
    /// bekleme cezasini sifirlamasin.
    /// </summary>
    [JsonPropertyName("kademe")] public int Tier { get; set; }

    /// <summary>Servisin en son gordugu yerel zaman; saat kaymasini anlamak icin.</summary>
    [JsonPropertyName("sonZaman")] public DateTime? LastKnownTime { get; set; }

    /// <summary>Ders saati takvimi. Bos ise takvim kurali isletilmez.</summary>
    [JsonPropertyName("takvim")] public List<WindowDto> Schedule { get; set; } = [];

    /// <summary>Bosta kalinca kilitlenme suresi (dakika). 0 ise kapali.</summary>
    [JsonPropertyName("bostaDk")] public int IdleLockMinutes { get; set; }

    public byte[] DecodeKey() => Base64Url.Decode(Key);

    public LockSchedule ToSchedule() => new(Schedule.Select(w => w.ToWindow()));

    public void SetSchedule(LockSchedule schedule) =>
        Schedule = schedule.Windows.Select(WindowDto.From).ToList();

    /// <summary>Takvim araliginin diskteki bicimi.</summary>
    public sealed class WindowDto
    {
        [JsonPropertyName("gunler")] public List<int> Days { get; set; } = [];
        [JsonPropertyName("bas")] public string Start { get; set; } = "00:00";
        [JsonPropertyName("bit")] public string End { get; set; } = "00:00";

        public static WindowDto From(AllowedWindow w) => new()
        {
            Days = w.Days.Select(d => (int)d).ToList(),
            Start = w.Start.ToString("HH:mm"),
            End = w.End.ToString("HH:mm"),
        };

        public AllowedWindow ToWindow() => new(
            Days.Select(d => (DayOfWeek)d).ToArray(),
            TimeOnly.ParseExact(Start, "HH:mm"),
            TimeOnly.ParseExact(End, "HH:mm"));
    }
}

/// <summary>
/// Kurulum PIN'i: yeni telefon eklemek, takvimi degistirmek ve kilidi kaldirmak
/// icin gerekir. PIN saklanmaz; PBKDF2 ozeti saklanir.
/// </summary>
public static class AdminPin
{
    private const int Iterations = 310_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    /// <summary>Kabul edilen en kisa PIN.</summary>
    public const int MinLength = 6;

    public static void Set(BoardConfig config, string pin)
    {
        if (pin.Length < MinLength)
            throw new ArgumentException($"PIN en az {MinLength} karakter olmali.", nameof(pin));

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        config.AdminPinSalt = Base64Url.Encode(salt);
        config.AdminPinHash = Base64Url.Encode(Derive(pin, salt));
    }

    public static bool Verify(BoardConfig config, string? pin)
    {
        if (string.IsNullOrEmpty(pin) ||
            string.IsNullOrEmpty(config.AdminPinSalt) ||
            string.IsNullOrEmpty(config.AdminPinHash))
        {
            return false;
        }

        var expected = Base64Url.Decode(config.AdminPinHash);
        var actual = Derive(pin, Base64Url.Decode(config.AdminPinSalt));

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] Derive(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin), salt, Iterations, HashAlgorithmName.SHA256, HashLength);
}

/// <summary>
/// Diske yazilan veriyi sifreler. Windows'ta DPAPI ile gerceklenir
/// (<c>ProtectedData</c>, makine kapsami); testlerde taklit edilir.
/// </summary>
public interface IDataProtector
{
    byte[] Protect(byte[] plain);

    /// <summary>Cozulemezse (baska makine, bozuk dosya) <c>null</c> doner.</summary>
    byte[]? Unprotect(byte[] protectedData);
}

/// <summary>Tahta yapilandirmasini sifreli olarak dosyada saklar.</summary>
public sealed class ConfigStore(string path, IDataProtector protector)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public string Path { get; } = path;

    public bool Exists => File.Exists(Path);

    /// <summary>Yapilandirmayi okur. Dosya yoksa veya cozulemiyorsa <c>null</c>.</summary>
    public BoardConfig? Load()
    {
        if (!Exists)
            return null;

        try
        {
            var plain = protector.Unprotect(File.ReadAllBytes(Path));
            if (plain is null)
                return null;

            return JsonSerializer.Deserialize<BoardConfig>(plain, Options);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Yapilandirmayi yazar. Once gecici dosyaya yazilip yer degistirilir:
    /// yazma sirasinda elektrik giderse eski dosya bozulmadan kalir.
    /// </summary>
    public void Save(BoardConfig config)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var bytes = protector.Protect(JsonSerializer.SerializeToUtf8Bytes(config, Options));
        var temp = Path + ".tmp";

        File.WriteAllBytes(temp, bytes);
        File.Move(temp, Path, overwrite: true);
    }
}
