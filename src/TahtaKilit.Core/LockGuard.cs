namespace TahtaKilit.Core;

/// <summary>Kilit acma denemesinin sonucu.</summary>
public enum UnlockOutcome
{
    /// <summary>Cevap dogru; kilit acilir.</summary>
    Success,

    /// <summary>Cevap yanlis. Yeni bir cagri uretildi, tekrar denenebilir.</summary>
    WrongCode,

    /// <summary>Cok fazla yanlis deneme yapildi; bekleme suresi dolmadan denenemez.</summary>
    TooManyAttempts,
}

/// <param name="Outcome">Denemenin sonucu.</param>
/// <param name="Wait">Bekleme varsa kalan sure.</param>
/// <param name="AttemptsLeft">Beklemeye girmeden once kalan deneme hakki.</param>
public readonly record struct UnlockResult(UnlockOutcome Outcome, TimeSpan Wait, int AttemptsLeft);

/// <summary>
/// Acilistan beri gecen sureyi veren saat. Duvar saati <em>bilerek</em>
/// kullanilmaz: tahtanin saati kayabildigi gibi, saati ileri alarak bekleme
/// cezasi da atlatilabilirdi.
/// </summary>
public interface IMonotonicClock
{
    long ElapsedMilliseconds { get; }
}

/// <inheritdoc />
public sealed class SystemMonotonicClock : IMonotonicClock
{
    public long ElapsedMilliseconds => Environment.TickCount64;
}

/// <summary>
/// Kilit ekraninin durum makinesi: acik cagriyi tutar, girilen cevabi dogrular,
/// yanlis denemelerde artan bekleme uygular.
/// </summary>
public sealed class LockGuard
{
    /// <summary>Beklemeye girmeden once verilen yanlis deneme hakki.</summary>
    public const int AttemptsPerRound = 5;

    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
    ];

    private readonly byte[] _key;
    private readonly string _boardId;
    private readonly IMonotonicClock _clock;
    private readonly Func<string> _challengeFactory;

    private int _failures;
    private long _lockedUntil;

    /// <param name="key">Tahtanin gizli anahtari.</param>
    /// <param name="boardId">Tahta kimligi.</param>
    /// <param name="initialTier">
    /// Onceki oturumdan devralinan ceza kademesi. Servis bunu diske yazip geri
    /// verirse, yeniden baslatarak bekleme cezasindan kacilamaz.
    /// </param>
    /// <param name="clock">Test icin degistirilebilir monotonik saat.</param>
    /// <param name="challengeFactory">Test icin degistirilebilir cagri ureteci.</param>
    public LockGuard(
        byte[] key,
        string boardId,
        int initialTier = 0,
        IMonotonicClock? clock = null,
        Func<string>? challengeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(boardId);

        _key = key;
        _boardId = boardId;
        _clock = clock ?? new SystemMonotonicClock();
        _challengeFactory = challengeFactory ?? UnlockProtocol.NewChallenge;

        Tier = Math.Clamp(initialTier, 0, Backoff.Length);
        CurrentChallenge = _challengeFactory();
    }

    /// <summary>Ekranda gosterilen gecerli cagri kodu.</summary>
    public string CurrentChallenge { get; private set; }

    /// <summary>Kilit ekranindaki QR'a yazilacak icerik.</summary>
    public string CurrentChallengeQr =>
        QrJson.Serialize(ChallengePayload.Create(_boardId, CurrentChallenge));

    /// <summary>Su anki ceza kademesi; servis bunu kalici saklamalidir.</summary>
    public int Tier { get; private set; }

    /// <summary>Bekleme varsa kalan sure, yoksa <see cref="TimeSpan.Zero"/>.</summary>
    public TimeSpan RemainingWait
    {
        get
        {
            var left = _lockedUntil - _clock.ElapsedMilliseconds;
            return left > 0 ? TimeSpan.FromMilliseconds(left) : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Girilen cevabi dogrular. Sonuc ne olursa olsun cagri yenilenir: boylece
    /// ayni cagri uzerinde deneme yapilamaz ve gorulen bir kod tekrar kullanilamaz.
    /// </summary>
    public UnlockResult TryUnlock(string? entered)
    {
        var wait = RemainingWait;
        if (wait > TimeSpan.Zero)
            return new UnlockResult(UnlockOutcome.TooManyAttempts, wait, 0);

        var ok = UnlockProtocol.VerifyResponse(_key, _boardId, CurrentChallenge, entered);
        Rotate();

        if (ok)
        {
            _failures = 0;
            Tier = 0;
            _lockedUntil = 0;
            return new UnlockResult(UnlockOutcome.Success, TimeSpan.Zero, AttemptsPerRound);
        }

        _failures++;
        if (_failures < AttemptsPerRound)
            return new UnlockResult(UnlockOutcome.WrongCode, TimeSpan.Zero, AttemptsPerRound - _failures);

        var penalty = Backoff[Math.Min(Tier, Backoff.Length - 1)];
        _failures = 0;
        Tier = Math.Min(Tier + 1, Backoff.Length);
        _lockedUntil = _clock.ElapsedMilliseconds + (long)penalty.TotalMilliseconds;

        return new UnlockResult(UnlockOutcome.TooManyAttempts, penalty, 0);
    }

    /// <summary>
    /// Yeni bir cagri uretir. Kilit ekrani yenilendiginde veya QR bir sure
    /// ekranda kaldiginda cagrilabilir.
    /// </summary>
    public void Rotate() => CurrentChallenge = _challengeFactory();
}
