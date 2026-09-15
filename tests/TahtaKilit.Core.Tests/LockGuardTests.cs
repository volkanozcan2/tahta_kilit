using TahtaKilit.Core;
using Xunit;

namespace TahtaKilit.Core.Tests;

/// <summary>Test icin elle ilerletilebilen monotonik saat.</summary>
internal sealed class FakeClock : IMonotonicClock
{
    public long ElapsedMilliseconds { get; private set; }

    public void Advance(TimeSpan by) => ElapsedMilliseconds += (long)by.TotalMilliseconds;
}

public class LockGuardTests
{
    private static LockGuard Olustur(FakeClock clock)
    {
        // Kodlar sirayla: 000000000001, 000000000002, ... boylece ongorulebilir.
        var sayac = 0;
        return new LockGuard(clock, () => (++sayac).ToString("D12"));
    }

    [Fact]
    public void Dogru_cevap_kilidi_acar()
    {
        var guard = Olustur(new FakeClock());

        var sonuc = guard.TryUnlock(XorProtocol.Solve(guard.CurrentCode));

        Assert.Equal(UnlockOutcome.Success, sonuc);
    }

    [Fact]
    public void Yanlis_cevap_reddedilir()
    {
        var guard = Olustur(new FakeClock());

        // 9999999 hicbir zaman dogru olamaz: en buyuk XOR sonucu 1048575.
        Assert.Equal(UnlockOutcome.WrongCode, guard.TryUnlock("9999999"));
    }

    [Fact]
    public void Yanlis_cevap_kodu_degistirmez()
    {
        // Ogretmen yanlis yazdiysa ekrandaki kod ayni kalmali; yoksa her
        // hatada karekodu yeniden okutmak gerekirdi.
        var guard = Olustur(new FakeClock());
        var kod = guard.CurrentCode;

        guard.TryUnlock("9999999");

        Assert.Equal(kod, guard.CurrentCode);
    }

    [Fact]
    public void Basarili_acilis_kodu_yeniler()
    {
        // Kilit tekrar kapandiginda eski cevap ise yaramamali.
        var guard = Olustur(new FakeClock());
        var kod = guard.CurrentCode;

        guard.TryUnlock(XorProtocol.Solve(kod));

        Assert.NotEqual(kod, guard.CurrentCode);
    }

    [Fact]
    public void Kod_otuz_saniye_sonra_yenilenir()
    {
        var clock = new FakeClock();
        var guard = Olustur(clock);
        var kod = guard.CurrentCode;

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(kod, guard.CurrentCode);

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.NotEqual(kod, guard.CurrentCode);
    }

    [Fact]
    public void Kalan_sure_geri_sayar()
    {
        var clock = new FakeClock();
        var guard = Olustur(clock);

        Assert.Equal(30, Math.Ceiling(guard.RemainingLife.TotalSeconds));

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(20, Math.Ceiling(guard.RemainingLife.TotalSeconds));
    }

    [Fact]
    public void Kod_yenilenince_kalan_sure_sifirlanir()
    {
        var clock = new FakeClock();
        var guard = Olustur(clock);

        clock.Advance(TimeSpan.FromSeconds(31));
        _ = guard.CurrentCode; // yenilenmeyi tetikler

        Assert.Equal(30, Math.Ceiling(guard.RemainingLife.TotalSeconds));
    }

    [Fact]
    public void Suresi_dolmus_kodun_cevabi_kabul_edilmez()
    {
        var clock = new FakeClock();
        var guard = Olustur(clock);
        var eskiCevap = XorProtocol.Solve(guard.CurrentCode);

        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(UnlockOutcome.WrongCode, guard.TryUnlock(eskiCevap));
    }
}
