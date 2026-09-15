using TahtaKilit.Core;
using Xunit;

namespace TahtaKilit.Core.Tests;

public class LockCoordinatorTests
{
    private static readonly byte[] Key = UnlockProtocol.NewKey();

    private static BoardConfig Config(int tier = 0) => new()
    {
        BoardId = "ABCDEFGH",
        BoardName = "Z-Blok 204",
        Key = Base64Url.Encode(Key),
        Tier = tier,
    };

    private static string DogruCevap(LockStatus durum) =>
        UnlockProtocol.ComputeResponse(Key, "ABCDEFGH", durum.Challenge);

    [Fact]
    public void Baslangicta_kilitlidir()
    {
        var k = new LockCoordinator(Config());

        var durum = k.Handle(new LockRequest(LockRequest.Durum));

        Assert.Equal(LockStatus.Kilitli, durum.Result);
        Assert.False(k.IsUnlocked);
        Assert.Equal("Z-Blok 204", durum.BoardName);
    }

    [Fact]
    public void Dogru_cevap_kilidi_acar()
    {
        var k = new LockCoordinator(Config());
        var durum = k.Handle(new LockRequest(LockRequest.Durum));

        var sonuc = k.Handle(new LockRequest(LockRequest.Ac, DogruCevap(durum)));

        Assert.Equal(LockStatus.Acildi, sonuc.Result);
        Assert.True(k.IsUnlocked);
    }

    [Fact]
    public void Yanlis_cevap_kalan_hakki_bildirir()
    {
        var k = new LockCoordinator(Config());

        var sonuc = k.Handle(new LockRequest(LockRequest.Ac, "00000000"));

        Assert.Equal(LockStatus.Yanlis, sonuc.Result);
        Assert.Equal(LockGuard.AttemptsPerRound - 1, sonuc.AttemptsLeft);
        Assert.False(k.IsUnlocked);
    }

    [Fact]
    public void Cok_yanlis_denemede_bekleme_suresi_bildirilir()
    {
        var k = new LockCoordinator(Config());

        LockStatus sonuc = default!;
        for (var i = 0; i < LockGuard.AttemptsPerRound; i++)
            sonuc = k.Handle(new LockRequest(LockRequest.Ac, "00000000"));

        Assert.Equal(LockStatus.Bekle, sonuc.Result);
        Assert.Equal(10, sonuc.WaitSeconds);
    }

    [Fact]
    public void Ceza_kademesi_diske_yazilmak_uzere_bildirilir()
    {
        var kademeler = new List<int>();
        var k = new LockCoordinator(Config(), onTierChanged: kademeler.Add);

        for (var i = 0; i < LockGuard.AttemptsPerRound; i++)
            k.Handle(new LockRequest(LockRequest.Ac, "00000000"));

        Assert.Equal([1], kademeler);
    }

    [Fact]
    public void Devralinan_kademe_ile_ceza_daha_uzun_baslar()
    {
        var k = new LockCoordinator(Config(tier: 3));

        LockStatus sonuc = default!;
        for (var i = 0; i < LockGuard.AttemptsPerRound; i++)
            sonuc = k.Handle(new LockRequest(LockRequest.Ac, "00000000"));

        Assert.Equal(300, sonuc.WaitSeconds); // 5 dakika
    }

    [Fact]
    public void Yenileme_yeni_cagri_uretir()
    {
        var k = new LockCoordinator(Config());
        var ilk = k.Handle(new LockRequest(LockRequest.Durum));

        var yeni = k.Handle(new LockRequest(LockRequest.Yenile));

        Assert.NotEqual(ilk.Challenge, yeni.Challenge);
    }

    [Fact]
    public void Yeniden_kilitleme_cagriyi_degistirir()
    {
        var k = new LockCoordinator(Config());
        var durum = k.Handle(new LockRequest(LockRequest.Durum));
        k.Handle(new LockRequest(LockRequest.Ac, DogruCevap(durum)));

        k.Lock();

        Assert.False(k.IsUnlocked);
        Assert.NotEqual(durum.Challenge, k.Handle(new LockRequest(LockRequest.Durum)).Challenge);
    }

    [Fact]
    public void Acildiktan_sonra_durum_acik_bildirir()
    {
        // Kilit ajani durumu bu yolla ogrenir; acikken kendini gizler.
        var k = new LockCoordinator(Config());
        var durum = k.Handle(new LockRequest(LockRequest.Durum));
        k.Handle(new LockRequest(LockRequest.Ac, DogruCevap(durum)));

        Assert.Equal(LockStatus.Acildi, k.Handle(new LockRequest(LockRequest.Durum)).Result);
    }

    [Fact]
    public void Ajan_kilitle_diyebilir()
    {
        // Bosta kalma veya elle kilitleme kisayolu bu istegi gonderir.
        var k = new LockCoordinator(Config());
        var durum = k.Handle(new LockRequest(LockRequest.Durum));
        k.Handle(new LockRequest(LockRequest.Ac, DogruCevap(durum)));

        var sonuc = k.Handle(new LockRequest(LockRequest.Kilitle));

        Assert.Equal(LockStatus.Kilitli, sonuc.Result);
        Assert.False(k.IsUnlocked);
        Assert.NotEqual(durum.Challenge, sonuc.Challenge);
    }

    [Fact]
    public void Bilinmeyen_istek_durum_gibi_islenir()
    {
        var k = new LockCoordinator(Config());

        Assert.Equal(LockStatus.Kilitli, k.Handle(new LockRequest("saçmalık")).Result);
    }

    [Fact]
    public void Qr_icerigi_gecerli_ve_cagriyla_tutarli()
    {
        var k = new LockCoordinator(Config());

        var durum = k.Handle(new LockRequest(LockRequest.Durum));
        var payload = QrJson.Deserialize<ChallengePayload>(durum.ChallengeQr);

        Assert.NotNull(payload);
        Assert.Equal(durum.Challenge, payload.C);
        Assert.Equal("ABCDEFGH", payload.Id);
    }

    [Fact]
    public void Mesajlar_json_uzerinden_gidip_gelebiliyor()
    {
        var k = new LockCoordinator(Config());

        // Kilit ekrani -> servis
        var istek = LockMessages.ParseRequest(LockMessages.Serialize(new LockRequest(LockRequest.Durum)));
        Assert.NotNull(istek);

        // Servis -> kilit ekrani
        var durum = LockMessages.ParseStatus(LockMessages.Serialize(k.Handle(istek)));
        Assert.NotNull(durum);
        Assert.Equal(UnlockProtocol.ChallengeLength, durum.Challenge.Length);
    }

    [Fact]
    public void Bozuk_mesaj_null_doner()
    {
        Assert.Null(LockMessages.ParseRequest("{bozuk"));
        Assert.Null(LockMessages.ParseStatus("bu json degil"));
    }
}

public class LockStatusTests
{
    [Fact]
    public void Bosta_kalma_suresi_kilit_ajanina_bildirilir()
    {
        // Ajan yapilandirmayi okuyamaz (dosya SYSTEM'e kapali); bu degeri
        // yalnizca servisten ogrenebilir.
        var config = new BoardConfig
        {
            BoardId = "ABCDEFGH",
            BoardName = "Z-Blok 204",
            Key = Base64Url.Encode(UnlockProtocol.NewKey()),
            IdleLockMinutes = 15,
        };

        var durum = new LockCoordinator(config).Handle(new LockRequest(LockRequest.Durum));

        Assert.Equal(15, durum.IdleLockMinutes);
    }
}
