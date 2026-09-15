using System.Text.Json;
using TahtaKilit.Core;
using Xunit;

namespace TahtaKilit.Core.Tests;

/// <summary>
/// Ortak vektorler: telefon tarafi (pwa/core.js) ile ayni sonucu urettigimizi
/// dogrular. Iki uygulama ayrisirsa ogretmen tahtayi acamaz, o yuzden bu test
/// projenin en kritik testidir.
/// </summary>
public class SharedVectorTests
{
    public sealed record Vector(string Note, string Key, string BoardId, string Challenge, string Response);

    public static TheoryData<Vector> Vectors
    {
        get
        {
            var json = File.ReadAllText("vectors.json");
            using var doc = JsonDocument.Parse(json);

            var data = new TheoryData<Vector>();
            foreach (var item in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                data.Add(new Vector(
                    item.GetProperty("note").GetString()!,
                    item.GetProperty("key").GetString()!,
                    item.GetProperty("boardId").GetString()!,
                    item.GetProperty("challenge").GetString()!,
                    item.GetProperty("response").GetString()!));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Telefon_ile_ayni_cevabi_uretir(Vector v)
    {
        var actual = UnlockProtocol.ComputeResponse(Base64Url.Decode(v.Key), v.BoardId, v.Challenge);
        Assert.Equal(v.Response, actual);
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Vektor_cevabi_dogrulanir(Vector v)
    {
        Assert.True(UnlockProtocol.VerifyResponse(
            Base64Url.Decode(v.Key), v.BoardId, v.Challenge, v.Response));
    }
}

public class ResponseTests
{
    private static readonly byte[] Key = Base64Url.Decode("JTA7RlFcZ3J9iJOeqbS_ytXg6_YBDBciLThDTllkb3o");
    private const string BoardId = "ABCDEFGH";

    [Fact]
    public void Cevap_sekiz_hanedir()
    {
        var response = UnlockProtocol.ComputeResponse(Key, BoardId, "4F7K2Q");
        Assert.Equal(8, response.Length);
        Assert.All(response, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Fact]
    public void Farkli_cagri_farkli_cevap_verir()
    {
        Assert.NotEqual(
            UnlockProtocol.ComputeResponse(Key, BoardId, "4F7K2Q"),
            UnlockProtocol.ComputeResponse(Key, BoardId, "4F7K2R"));
    }

    [Fact]
    public void Cevap_tahtaya_baglidir()
    {
        // Ayni anahtar baska bir tahtada baska cevap uretir: cevap kopyalanamaz.
        Assert.NotEqual(
            UnlockProtocol.ComputeResponse(Key, "ABCDEFGH", "4F7K2Q"),
            UnlockProtocol.ComputeResponse(Key, "ABCDEFGJ", "4F7K2Q"));
    }

    [Fact]
    public void Yanlis_anahtar_dogrulanmaz()
    {
        var response = UnlockProtocol.ComputeResponse(Key, BoardId, "4F7K2Q");
        var baskaAnahtar = UnlockProtocol.NewKey();
        Assert.False(UnlockProtocol.VerifyResponse(baskaAnahtar, BoardId, "4F7K2Q", response));
    }

    [Theory]
    [InlineData("42 63 04 48")]
    [InlineData("4263-0448")]
    [InlineData(" 42630448 ")]
    public void Bosluk_ve_tire_gormezden_gelinir(string entered)
    {
        Assert.True(UnlockProtocol.VerifyResponse(Key, BoardId, "4F7K2Q", entered));
    }

    [Theory]
    [InlineData("")]
    [InlineData("4263044")]      // eksik hane
    [InlineData("426304489")]    // fazla hane
    [InlineData("4263O448")]     // harf
    [InlineData(null)]
    public void Bozuk_giris_reddedilir(string? entered)
    {
        Assert.False(UnlockProtocol.VerifyResponse(Key, BoardId, "4F7K2Q", entered));
    }

    [Fact]
    public void Cagri_kucuk_harf_ve_karistirilan_harflerle_de_calisir()
    {
        // Ogretmen "4f7k2q" yazarsa da, "I" yerine "1" gorurse de ayni sonuc.
        var beklenen = UnlockProtocol.ComputeResponse(Key, BoardId, "4F7K2Q");
        Assert.Equal(beklenen, UnlockProtocol.ComputeResponse(Key, "abcdefgh", "4f7k2q"));
    }

    [Fact]
    public void Ekran_bicimi_ikiserli_gruplar()
    {
        Assert.Equal("42 63 04 48", UnlockProtocol.FormatForDisplay("42630448"));
    }

    [Fact]
    public void Uretilen_cagrilar_farklidir()
    {
        var cagrilar = Enumerable.Range(0, 200).Select(_ => UnlockProtocol.NewChallenge()).ToList();
        Assert.All(cagrilar, c => Assert.Equal(UnlockProtocol.ChallengeLength, c.Length));
        Assert.True(cagrilar.Distinct().Count() > 190, "Cagrilar yeterince rastgele degil.");
    }

    [Fact]
    public void Uretilen_kimlikler_gecerlidir()
    {
        var id = UnlockProtocol.NewBoardId();
        Assert.Equal(UnlockProtocol.BoardIdLength, id.Length);
        Assert.True(Crockford32.TryNormalize(id, UnlockProtocol.BoardIdLength, out var n));
        Assert.Equal(id, n);
    }
}
