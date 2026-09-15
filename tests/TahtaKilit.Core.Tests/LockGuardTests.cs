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
    private static readonly byte[] Key = Base64Url.Decode("JTA7RlFcZ3J9iJOeqbS_ytXg6_YBDBciLThDTllkb3o");
    private const string BoardId = "ABCDEFGH";

    private static LockGuard Olustur(FakeClock clock, int tier = 0)
    {
        // Cagrilar sirayla: 000001, 000002, ... boylece test ongorulebilir olur.
        var sayac = 0;
        return new LockGuard(Key, BoardId, tier, clock,
            () => (++sayac).ToString("D6"));
    }

    private static string DogruCevap(LockGuard guard) =>
        UnlockProtocol.ComputeResponse(Key, BoardId, guard.CurrentChallenge);

    [Fact]
    public void Dogru_cevap_kilidi_acar()
    {
        var guard = Olustur(new FakeClock());
        Assert.Equal(UnlockOutcome.Success, guard.TryUnlock(DogruCevap(guard)).Outcome);
    }

    [Fact]
    public void Cagri_her_denemeden_sonra_degisir()
    {
        var guard = Olustur(new FakeClock());
        var ilk = guard.CurrentChallenge;

        guard.TryUnlock("00000000");

        Assert.NotEqual(ilk, guard.CurrentChallenge);
    }

    [Fact]
    public void Kullanilan_cevap_ikinci_kez_ise_yaramaz()
    {
        // Omuz ustunden kodu goren biri sonra ayni kodu kullanamamali.
        var guard = Olustur(new FakeClock());
        var cevap = DogruCevap(guard);

        Assert.Equal(UnlockOutcome.Success, guard.TryUnlock(cevap).Outcome);
        Assert.Equal(UnlockOutcome.WrongCode, guard.TryUnlock(cevap).Outcome);
    }

    [Fact]
    public void Yanlis_denemede_kalan_hak_azalir()
    {
        var guard = Olustur(new FakeClock());

        var sonuc = guard.TryUnlock("00000000");

        Assert.Equal(UnlockOutcome.WrongCode, sonuc.Outcome);
        Assert.Equal(LockGuard.AttemptsPerRound - 1, sonuc.AttemptsLeft);
    }

    [Fact]
    public void Bes_yanlis_denemeden_sonra_beklemeye_girer()
    {
        var guard = Olustur(new FakeClock());

        for (var i = 0; i < LockGuard.AttemptsPerRound - 1; i++)
            Assert.Equal(UnlockOutcome.WrongCode, guard.TryUnlock("00000000").Outcome);

        var sonuc = guard.TryUnlock("00000000");

        Assert.Equal(UnlockOutcome.TooManyAttempts, sonuc.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(10), sonuc.Wait);
    }

    [Fact]
    public void Bekleme_sirasinda_dogru_cevap_bile_kabul_edilmez()
    {
        var clock = new FakeClock();
        var guard = Olustur(clock);
        BeklemeyeSok(guard);

        var sonuc = guard.TryUnlock(DogruCevap(guard));

        Assert.Equal(UnlockOutcome.TooManyAttempts, sonuc.Outcome);
    }

    [Fact]
    public void Bekleme_dolunca_tekrar_denenebilir()
    {
        var clock = new FakeClock();
        var guard = Olustur(clock);
        BeklemeyeSok(guard);

        clock.Advance(TimeSpan.FromSeconds(11));

        Assert.Equal(UnlockOutcome.Success, guard.TryUnlock(DogruCevap(guard)).Outcome);
    }

    [Fact]
    public void Bekleme_suresi_her_turda_artar()
    {
        var clock = new FakeClock();
        var guard = Olustur(clock);

        TimeSpan[] beklenen =
        [
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5), // tavan
        ];

        foreach (var sure in beklenen)
        {
            var sonuc = BeklemeyeSok(guard);
            Assert.Equal(sure, sonuc.Wait);
            clock.Advance(sure + TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public void Basarili_acilis_cezayi_sifirlar()
    {
        var clock = new FakeClock();
        var guard = Olustur(clock);

        BeklemeyeSok(guard);
        clock.Advance(TimeSpan.FromSeconds(11));
        guard.TryUnlock(DogruCevap(guard));

        Assert.Equal(0, guard.Tier);
        Assert.Equal(TimeSpan.FromSeconds(10), BeklemeyeSok(guard).Wait);
    }

    [Fact]
    public void Devralinan_ceza_kademesi_yeniden_baslatmayla_atlatilamaz()
    {
        // Servis kademeyi diske yazip geri verir; tahtayi kapatip acmak
        // bekleme cezasini sifirlamamali.
        var guard = Olustur(new FakeClock(), tier: 3);

        Assert.Equal(TimeSpan.FromMinutes(5), BeklemeyeSok(guard).Wait);
    }

    [Fact]
    public void Kilit_ekrani_qr_icerigi_cozulebilir()
    {
        var guard = Olustur(new FakeClock());

        var payload = QrJson.Deserialize<ChallengePayload>(guard.CurrentChallengeQr);

        Assert.NotNull(payload);
        Assert.Equal(ChallengePayload.CurrentVersion, payload.V);
        Assert.Equal(BoardId, payload.Id);
        Assert.Equal(guard.CurrentChallenge, payload.C);
    }

    private static UnlockResult BeklemeyeSok(LockGuard guard)
    {
        UnlockResult sonuc = default;
        for (var i = 0; i < LockGuard.AttemptsPerRound; i++)
            sonuc = guard.TryUnlock("00000000");

        return sonuc;
    }
}

public class QrPayloadTests
{
    [Fact]
    public void Eslestirme_qr_donup_gelince_ayni_anahtari_verir()
    {
        var key = UnlockProtocol.NewKey();
        var id = UnlockProtocol.NewBoardId();

        var json = QrJson.Serialize(PairingPayload.Create(id, "Z-Blok 204", key));
        var geri = QrJson.Deserialize<PairingPayload>(json);

        Assert.NotNull(geri);
        Assert.Equal(id, geri.Id);
        Assert.Equal("Z-Blok 204", geri.Ad);
        Assert.Equal(key, geri.DecodeKey());
    }

    [Fact]
    public void Bozuk_json_null_doner()
    {
        Assert.Null(QrJson.Deserialize<PairingPayload>("bu json degil"));
    }

    [Fact]
    public void Base64url_tur_gidis_donus()
    {
        var bytes = UnlockProtocol.NewKey();
        var encoded = Base64Url.Encode(bytes);

        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
        Assert.Equal(bytes, Base64Url.Decode(encoded));
    }
}

public class Crockford32Tests
{
    [Theory]
    [InlineData("4f7k2q", "4F7K2Q")]      // kucuk harf
    [InlineData("4F7K 2Q", "4F7K2Q")]     // bosluk
    [InlineData("4F7K-2Q", "4F7K2Q")]     // tire
    [InlineData("4F7K2O", "4F7K20")]      // O harfi -> sifir
    [InlineData("4F7KI Q", "4F7K1Q")]     // I harfi -> bir
    [InlineData("4F7KLQ", "4F7K1Q")]      // L harfi -> bir
    public void Karistirilan_karakterler_duzeltilir(string girilen, string beklenen)
    {
        Assert.True(Crockford32.TryNormalize(girilen, 6, out var sonuc));
        Assert.Equal(beklenen, sonuc);
    }

    [Theory]
    [InlineData("4F7K2")]     // kisa
    [InlineData("4F7K2QQ")]   // uzun
    [InlineData("4F7K2U")]    // U alfabede yok
    [InlineData("4F7K2!")]    // gecersiz karakter
    [InlineData("")]
    [InlineData(null)]
    public void Gecersiz_kod_reddedilir(string? girilen)
    {
        Assert.False(Crockford32.TryNormalize(girilen, 6, out _));
    }

    [Fact]
    public void Kodlama_sabit_uzunluktadir()
    {
        Assert.Equal("000000", Crockford32.Encode(0, 6));
        Assert.Equal("000001", Crockford32.Encode(1, 6));
        Assert.Equal("ZZZZZZ", Crockford32.Encode((1UL << 30) - 1, 6));
    }
}
