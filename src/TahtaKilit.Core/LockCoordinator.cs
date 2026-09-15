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
    public const string Yenile = "yenile";

    /// <summary>Kilit ajani "simdi kilitle" der: bosta kalma veya elle kilitleme.</summary>
    public const string Kilitle = "kilitle";
}

/// <summary>Servisin kilit ekranina verdigi cevap.</summary>
public sealed record LockStatus(
    [property: JsonPropertyName("sonuc")] string Result,
    [property: JsonPropertyName("tahta")] string BoardName,
    [property: JsonPropertyName("cagri")] string Challenge,
    [property: JsonPropertyName("qr")] string ChallengeQr,
    [property: JsonPropertyName("beklemeSn")] int WaitSeconds,
    [property: JsonPropertyName("kalanHak")] int AttemptsLeft,
    [property: JsonPropertyName("bostaDk")] int IdleLockMinutes = 0)
{
    public const string Kilitli = "kilitli";
    public const string Acildi = "acildi";
    public const string Yanlis = "yanlis";
    public const string Bekle = "bekle";
}

/// <summary>
/// Kilit ekrani ile dogrulama mantigi arasindaki koprü.
///
/// Gizli anahtar yalnizca bu nesnede, yani SYSTEM olarak calisan serviste
/// bulunur. Kullanici oturumundaki kilit ekrani anahtari hicbir zaman gormez;
/// sadece girilen yaziyi iletir ve "acildi / yanlis / bekle" cevabini alir.
/// </summary>
public sealed class LockCoordinator
{
    private readonly LockGuard _guard;
    private readonly string _boardName;
    private readonly int _idleLockMinutes;
    private readonly Action<int>? _onTierChanged;

    private int _lastTier;

    /// <param name="onTierChanged">
    /// Ceza kademesi degistiginde cagrilir; servis bunu diske yazar ki
    /// tahtayi yeniden baslatmak bekleme cezasini sifirlamasin.
    /// </param>
    public LockCoordinator(BoardConfig config, IMonotonicClock? clock = null, Action<int>? onTierChanged = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        _boardName = config.BoardName;
        _idleLockMinutes = config.IdleLockMinutes;
        _onTierChanged = onTierChanged;
        _guard = new LockGuard(config.DecodeKey(), config.BoardId, config.Tier, clock);
        _lastTier = _guard.Tier;
    }

    /// <summary>Kilit su anda acik mi?</summary>
    public bool IsUnlocked { get; private set; }

    public LockStatus Handle(LockRequest request) => request.Op switch
    {
        LockRequest.Durum => Snapshot(CurrentState),
        LockRequest.Yenile => Refresh(),
        LockRequest.Ac => Unlock(request.Response),
        LockRequest.Kilitle => LockAndReport(),
        _ => Snapshot(CurrentState),
    };

    private string CurrentState => IsUnlocked ? LockStatus.Acildi : LockStatus.Kilitli;

    private LockStatus LockAndReport()
    {
        Lock();
        return Snapshot(LockStatus.Kilitli);
    }

    /// <summary>Tahtayi yeniden kilitler (takvim, bosta kalma veya elle kilitleme).</summary>
    public void Lock()
    {
        IsUnlocked = false;
        _guard.Rotate();
    }

    private LockStatus Refresh()
    {
        // Cagri ekranda uzun sure bekledi; yenisini uret.
        _guard.Rotate();
        return Snapshot(CurrentState);
    }

    private LockStatus Unlock(string? response)
    {
        var sonuc = _guard.TryUnlock(response);

        if (_guard.Tier != _lastTier)
        {
            _lastTier = _guard.Tier;
            _onTierChanged?.Invoke(_lastTier);
        }

        switch (sonuc.Outcome)
        {
            case UnlockOutcome.Success:
                IsUnlocked = true;
                return Snapshot(LockStatus.Acildi, sonuc);

            case UnlockOutcome.TooManyAttempts:
                return Snapshot(LockStatus.Bekle, sonuc);

            default:
                return Snapshot(LockStatus.Yanlis, sonuc);
        }
    }

    private LockStatus Snapshot(string result, UnlockResult? sonuc = null) => new(
        result,
        _boardName,
        _guard.CurrentChallenge,
        _guard.CurrentChallengeQr,
        (int)Math.Ceiling((sonuc?.Wait ?? _guard.RemainingWait).TotalSeconds),
        sonuc?.AttemptsLeft ?? LockGuard.AttemptsPerRound,
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
