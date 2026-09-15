using System.Text.Json;
using System.Text.Json.Serialization;

namespace TahtaKilit.Core;

/// <summary>
/// Tahtanin kalici yapilandirmasi.
///
/// Gizli anahtar yoktur; kurulumda yalnizca tahtanin adi sorulur. Dosya yine
/// de sifreli yazilir (bkz. <see cref="ConfigStore"/>), ama bu bir sir
/// saklamak icin degil, ayarlarin elle kurcalanmasini zorlastirmak icindir.
/// </summary>
public sealed class BoardConfig
{
    public const int CurrentVersion = 2;

    [JsonPropertyName("v")] public int V { get; set; } = CurrentVersion;

    /// <summary>Kilit ekraninda gorunen ad, ornegin "Z-Blok 204".</summary>
    [JsonPropertyName("ad")] public string BoardName { get; set; } = "";

    /// <summary>Servisin en son gordugu yerel zaman; saat kaymasini anlamak icin.</summary>
    [JsonPropertyName("sonZaman")] public DateTime? LastKnownTime { get; set; }

    /// <summary>Ders saati takvimi. Bos ise takvim kurali isletilmez.</summary>
    [JsonPropertyName("takvim")] public List<WindowDto> Schedule { get; set; } = [];

    /// <summary>Bosta kalinca kilitlenme suresi (dakika). 0 ise kapali.</summary>
    [JsonPropertyName("bostaDk")] public int IdleLockMinutes { get; set; }

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
            Start = w.Start.ToString("HH\\:mm"),
            End = w.End.ToString("HH\\:mm"),
        };

        public AllowedWindow ToWindow() => new(
            Days.Select(d => (DayOfWeek)d).ToArray(),
            TimeOnly.ParseExact(Start, "HH\\:mm"),
            TimeOnly.ParseExact(End, "HH\\:mm"));
    }
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
