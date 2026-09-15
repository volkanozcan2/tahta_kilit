namespace TahtaKilit.Core;

/// <summary>
/// Tahtanin kullanilabildigi bir zaman araligi, ornegin hafta ici 07:30-17:00.
/// Bitis baslangictan kucukse aralik gece yarisini asar (22:00-06:00 gibi).
/// </summary>
public sealed record AllowedWindow(DayOfWeek[] Days, TimeOnly Start, TimeOnly End)
{
    public bool Contains(DateTime local)
    {
        var time = TimeOnly.FromDateTime(local);

        if (Start <= End)
            return Days.Contains(local.DayOfWeek) && time >= Start && time < End;

        // Gece yarisini asan aralik: aksam kismi bugunun, sabah kismi dunun gunudur.
        if (time >= Start)
            return Days.Contains(local.DayOfWeek);

        return time < End && Days.Contains(local.AddDays(-1).DayOfWeek);
    }
}

/// <summary>
/// Ders saati takvimi.
///
/// Takvim tahtayi yalnizca <em>kilitleyebilir</em>, asla kendi basina acamaz.
/// Bu bilincli bir tercihtir: tahtanin saati kayabildigi icin, saate bakip
/// kilidi acan bir kural guvenlik acigi olurdu. En kotu ihtimalle tahta
/// beklenmedik anda kilitlenir; ogretmen telefonuyla saniyeler icinde acar.
/// </summary>
public sealed class LockSchedule
{
    /// <summary>Saatin kaydigina karar vermek icin tolerans.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromHours(2);

    private readonly AllowedWindow[] _windows;

    public LockSchedule(IEnumerable<AllowedWindow>? windows = null)
        => _windows = windows?.ToArray() ?? [];

    /// <summary>Takvim tanimlanmamissa kural isletilmez.</summary>
    public bool IsEmpty => _windows.Length == 0;

    public IReadOnlyList<AllowedWindow> Windows => _windows;

    /// <summary>Hafta ici mesai: Pazartesi-Cuma 07:30-17:00.</summary>
    public static LockSchedule WeekdaysSchoolHours() => new(
    [
        new AllowedWindow(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
             DayOfWeek.Thursday, DayOfWeek.Friday],
            new TimeOnly(7, 30), new TimeOnly(17, 0)),
    ]);

    /// <summary>
    /// Verilen anda tahtanin kilitlenmesi gerekiyor mu?
    ///
    /// <paramref name="lastKnownTime"/> verilirse ve sistem saati ondan
    /// belirgin sekilde <em>geriye</em> gitmisse saat guvenilmez sayilir ve
    /// takvim isletilmez — yanlis saat yuzunden ders ortasinda kilitlenme olmaz.
    /// Guvenlik kaybi yoktur: takvim zaten hicbir zaman kilit acmaz.
    /// </summary>
    public bool MustLockAt(DateTime local, DateTime? lastKnownTime = null)
    {
        if (IsEmpty || IsClockSuspicious(local, lastKnownTime))
            return false;

        return !_windows.Any(w => w.Contains(local));
    }

    /// <summary>
    /// Sistem saati son bilinen zamandan geriye gitmisse saat kaymis demektir.
    /// Admin ekraninda uyari gostermek icin de kullanilir.
    /// </summary>
    public static bool IsClockSuspicious(DateTime local, DateTime? lastKnownTime) =>
        lastKnownTime is { } son && local < son - ClockTolerance;
}
