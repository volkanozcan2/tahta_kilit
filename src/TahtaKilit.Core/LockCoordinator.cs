using System.Text.Json;
using System.Text.Json.Serialization;

namespace TahtaKilit.Core;

/// <summary>Kilit ekraninin servise sordugu soru.</summary>
public sealed record LockRequest(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("cevap")] string? Response = null)
{
    public const string Durum = "durum";
    public const string Ac = "ac";

    /// <summary>Kilit ajani "simdi kilitle" der: bosta kalma veya elle kilitleme.</summary>
    public const string Kilitle = "kilitle";
}

/// <summary>Servisin kilit ekranina verdigi cevap.</summary>
public sealed record LockStatus(
    [property: JsonPropertyName("sonuc")] string Result,
    [property: JsonPropertyName("tahta")] string BoardName,
    [property: JsonPropertyName("kod")] string Code,
    [property: JsonPropertyName("kalanSn")] int RemainingSeconds,
    [property: JsonPropertyName("bostaDk")] int IdleLockMinutes = 0)
{
    public const string Kilitli = "kilitli";
    public const string Acildi = "acildi";
    public const string Yanlis = "yanlis";
}

/// <summary>
/// Kilit ekrani ile dogrulama mantigi arasindaki koprü.
///
/// Dogrulama SYSTEM olarak calisan serviste yapilir; kilit ekrani girilen
/// yaziyi iletip sonucu alir. (Bu kuralda gizli anahtar olmadigi icin bu
/// ayrim guvenlik saglamaz, ama kilit durumunun tek bir yerde tutulmasini
/// saglar: ekran oldurulup yeniden baslatilsa da kilit acik kalmaz.)
/// </summary>
public sealed class LockCoordinator
{
    private readonly LockGuard _guard;
    private readonly string _boardName;
    private readonly int _idleLockMinutes;

    public LockCoordinator(BoardConfig config, IMonotonicClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        _boardName = config.BoardName;
        _idleLockMinutes = config.IdleLockMinutes;
        _guard = new LockGuard(clock);
    }

    /// <summary>Kilit su anda acik mi?</summary>
    public bool IsUnlocked { get; private set; }

    public LockStatus Handle(LockRequest request) => request.Op switch
    {
        LockRequest.Ac => Unlock(request.Response),
        LockRequest.Kilitle => LockAndReport(),
        _ => Snapshot(CurrentState),
    };

    /// <summary>Tahtayi yeniden kilitler (takvim, bosta kalma veya elle kilitleme).</summary>
    public void Lock()
    {
        IsUnlocked = false;
        _guard.Rotate();
    }

    private string CurrentState => IsUnlocked ? LockStatus.Acildi : LockStatus.Kilitli;

    private LockStatus LockAndReport()
    {
        Lock();
        return Snapshot(LockStatus.Kilitli);
    }

    private LockStatus Unlock(string? response)
    {
        if (_guard.TryUnlock(response) == UnlockOutcome.Success)
        {
            IsUnlocked = true;
            return Snapshot(LockStatus.Acildi);
        }

        return Snapshot(LockStatus.Yanlis);
    }

    private LockStatus Snapshot(string result) => new(
        result,
        _boardName,
        _guard.CurrentCode,
        (int)Math.Ceiling(_guard.RemainingLife.TotalSeconds),
        // Kilit ajani yapilandirmayi okuyamaz (dosya SYSTEM'e kapali),
        // bosta kalma suresini servisten ogrenir.
        _idleLockMinutes);
}

/// <summary>Kilit ekrani ile servis arasindaki mesajlarin JSON bicimi.</summary>
public static class LockMessages
{
    private static readonly JsonSerializerOptions Options = new();

    public static string Serialize(LockRequest request) => JsonSerializer.Serialize(request, Options);

    public static string Serialize(LockStatus status) => JsonSerializer.Serialize(status, Options);

    public static LockRequest? ParseRequest(string json) => Parse<LockRequest>(json);

    public static LockStatus? ParseStatus(string json) => Parse<LockStatus>(json);

    private static T? Parse<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
