using TahtaKilit.Core;
using Xunit;

namespace TahtaKilit.Core.Tests;

public class LockCoordinatorTests
{
    private static BoardConfig Config(int bostaDk = 0) => new()
    {
        BoardName = "Z-Blok 204",
        IdleLockMinutes = bostaDk,
    };

    private static string DogruCevap(LockStatus durum) => XorProtocol.Solve(durum.Code);

    [Fact]
    public void Baslangicta_kilitlidir()
    {
        var k = new LockCoordinator(Config());

        var durum = k.Handle(new LockRequest(LockRequest.Durum));

        Assert.Equal(LockStatus.Kilitli, durum.Result);
        Assert.False(k.IsUnlocked);
        Assert.Equal("Z-Blok 204", durum.BoardName);
        Assert.Equal(XorProtocol.CodeDigits, durum.Code.Length);
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
    public void Yanlis_cevap_kilidi_acmaz()
    {
        var k = new LockCoordinator(Config());

        // 9999999 hicbir zaman dogru olamaz: en buyuk XOR sonucu 1048575.
        var sonuc = k.Handle(new LockRequest(LockRequest.Ac, "9999999"));

        Assert.Equal(LockStatus.Yanlis, sonuc.Result);
        Assert.False(k.IsUnlocked);
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
        var k = new LockCoordinator(Config());
        var durum = k.Handle(new LockRequest(LockRequest.Durum));
        k.Handle(new LockRequest(LockRequest.Ac, DogruCevap(durum)));

        var sonuc = k.Handle(new LockRequest(LockRequest.Kilitle));

        Assert.Equal(LockStatus.Kilitli, sonuc.Result);
        Assert.False(k.IsUnlocked);
        Assert.NotEqual(durum.Code, sonuc.Code);
    }

    [Fact]
    public void Kullanilan_cevap_kilit_kapaninca_ise_yaramaz()
    {
        var k = new LockCoordinator(Config());
        var durum = k.Handle(new LockRequest(LockRequest.Durum));
        var cevap = DogruCevap(durum);

        k.Handle(new LockRequest(LockRequest.Ac, cevap));
        k.Handle(new LockRequest(LockRequest.Kilitle));

        Assert.Equal(LockStatus.Yanlis, k.Handle(new LockRequest(LockRequest.Ac, cevap)).Result);
    }

    [Fact]
    public void Bilinmeyen_istek_durum_gibi_islenir()
    {
        var k = new LockCoordinator(Config());

        Assert.Equal(LockStatus.Kilitli, k.Handle(new LockRequest("saçmalık")).Result);
    }

    [Fact]
    public void Bosta_kalma_suresi_kilit_ajanina_bildirilir()
    {
        // Ajan yapilandirmayi okuyamaz; bu degeri yalnizca servisten ogrenir.
        var durum = new LockCoordinator(Config(bostaDk: 15)).Handle(new LockRequest(LockRequest.Durum));

        Assert.Equal(15, durum.IdleLockMinutes);
    }

    [Fact]
    public void Mesajlar_json_uzerinden_gidip_gelebiliyor()
    {
        var k = new LockCoordinator(Config());

        var istek = LockMessages.ParseRequest(LockMessages.Serialize(new LockRequest(LockRequest.Durum)));
        Assert.NotNull(istek);

        var durum = LockMessages.ParseStatus(LockMessages.Serialize(k.Handle(istek)));
        Assert.NotNull(durum);
        Assert.Equal(XorProtocol.CodeDigits, durum.Code.Length);
    }

    [Fact]
    public void Bozuk_mesaj_null_doner()
    {
        Assert.Null(LockMessages.ParseRequest("{bozuk"));
        Assert.Null(LockMessages.ParseStatus("bu json degil"));
    }
}
