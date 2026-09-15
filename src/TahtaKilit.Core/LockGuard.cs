namespace TahtaKilit.Core;

/// <summary>Kilit acma denemesinin sonucu.</summary>
public enum UnlockOutcome
{
    /// <summary>Cevap dogru; kilit acilir.</summary>
    Success,

    /// <summary>Cevap yanlis.</summary>
    WrongCode,
}

/// <summary>
/// Acilistan beri gecen sureyi veren saat. Duvar saati kullanilmaz: tahtanin
/// saati kayabilir, kodun omru ise kaymadan olculmelidir.
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
/// Kilit ekraninin durum makinesi: ekranda gosterilen kodu tutar, omru
/// dolunca yenisini uretir ve girilen cevabi dogrular.
/// </summary>
public sealed class LockGuard
{
    private readonly IMonotonicClock _clock;
    private readonly Func<string> _codeFactory;

    private string _code;
    private long _generatedAt;

    /// <param name="clock">Test icin degistirilebilir monotonik saat.</param>
    /// <param name="codeFactory">Test icin degistirilebilir kod ureteci.</param>
    public LockGuard(IMonotonicClock? clock = null, Func<string>? codeFactory = null)
    {
        _clock = clock ?? new SystemMonotonicClock();
        _codeFactory = codeFactory ?? XorProtocol.NewCode;

        _code = _codeFactory();
        _generatedAt = _clock.ElapsedMilliseconds;
    }

    /// <summary>
    /// Ekranda gosterilen gecerli kod. Omru dolmussa okunurken yenilenir.
    /// </summary>
    public string CurrentCode
    {
        get
        {
            RotateIfExpired();
            return _code;
        }
    }

    /// <summary>Gecerli kodun yenilenmesine kalan sure.</summary>
    public TimeSpan RemainingLife
    {
        get
        {
            RotateIfExpired();
            var gecen = _clock.ElapsedMilliseconds - _generatedAt;
            var kalan = XorProtocol.CodeLifetime.TotalMilliseconds - gecen;
            return kalan > 0 ? TimeSpan.FromMilliseconds(kalan) : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Girilen cevabi dogrular. Dogruysa kod yenilenir; boylece kilit tekrar
    /// kapandiginda eski cevap ise yaramaz.
    /// </summary>
    public UnlockOutcome TryUnlock(string? entered)
    {
        if (!XorProtocol.Verify(CurrentCode, entered))
            return UnlockOutcome.WrongCode;

        Rotate();
        return UnlockOutcome.Success;
    }

    /// <summary>Yeni bir kod uretir.</summary>
    public void Rotate()
    {
        _code = _codeFactory();
        _generatedAt = _clock.ElapsedMilliseconds;
    }

    private void RotateIfExpired()
    {
        if (_clock.ElapsedMilliseconds - _generatedAt >= XorProtocol.CodeLifetime.TotalMilliseconds)
            Rotate();
    }
}
