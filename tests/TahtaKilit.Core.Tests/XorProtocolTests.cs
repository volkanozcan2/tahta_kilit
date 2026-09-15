using System.Text.Json;
using TahtaKilit.Core;
using Xunit;

namespace TahtaKilit.Core.Tests;

/// <summary>
/// Ortak vektorler: telefon tarafi (pwa/core.js) ile ayni cevabi urettigimizi
/// dogrular. Ikisi ayrisirsa ogretmen tahtayi acamaz.
/// </summary>
public class SharedVectorTests
{
    public sealed record Vector(string Note, string Code, string Answer);

    public static TheoryData<Vector> Vectors
    {
        get
        {
            using var doc = JsonDocument.Parse(File.ReadAllText("vectors.json"));

            var data = new TheoryData<Vector>();
            foreach (var item in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                data.Add(new Vector(
                    item.GetProperty("not").GetString()!,
                    item.GetProperty("kod").GetString()!,
                    item.GetProperty("cevap").GetString()!));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Telefon_ile_ayni_cevabi_uretir(Vector v)
    {
        Assert.Equal(v.Answer, XorProtocol.Solve(v.Code));
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Vektor_cevabi_dogrulanir(Vector v)
    {
        Assert.True(XorProtocol.Verify(v.Code, v.Answer));
    }
}

public class XorSolveTests
{
    [Theory]
    [InlineData("123456654321", 123456 ^ 654321)]
    [InlineData("000000000000", 0)]
    [InlineData("999999999999", 0)]           // ayni sayilarin XOR'u sifir
    [InlineData("000000123456", 123456)]      // sifirla XOR degistirmez
    [InlineData("123456000000", 123456)]
    public void Cevap_ilk_ve_son_yarimin_xoru(string kod, int beklenen)
    {
        Assert.Equal(beklenen.ToString("D7"), XorProtocol.Solve(kod));
    }

    [Fact]
    public void Cevap_hep_yedi_hanedir()
    {
        // Basa sifir konur; ekranda hep ayni genislikte gorunmeli.
        Assert.Equal("0000000", XorProtocol.Solve("999999999999"));
        Assert.Equal(XorProtocol.AnswerDigits, XorProtocol.Solve("000001000002").Length);
    }

    [Fact]
    public void En_buyuk_cevap_yedi_haneye_sigar()
    {
        // 999999 ^ 000000 gibi degil; gercek tavan 1048575 (2^20 - 1).
        var enBuyuk = 0;
        for (var a = 0; a <= 999999; a += 9973)
        {
            for (var b = 0; b <= 999999; b += 9973)
                enBuyuk = Math.Max(enBuyuk, a ^ b);
        }

        Assert.True(enBuyuk < 10_000_000, "Cevap yedi haneye sigmali.");
    }

    [Fact]
    public void Xor_simetriktir()
    {
        // Yarimlarin yeri degisince cevap degismez.
        Assert.Equal(XorProtocol.Solve("123456654321"), XorProtocol.Solve("654321123456"));
    }

    [Theory]
    [InlineData("12345665432")]    // eksik hane
    [InlineData("1234566543210")]  // fazla hane
    [InlineData("12345665432A")]   // harf
    [InlineData("")]
    public void Bozuk_kod_reddedilir(string kod)
    {
        Assert.Throws<ArgumentException>(() => XorProtocol.Solve(kod));
    }

    [Fact]
    public void Uretilen_kodlar_on_iki_hane_ve_farkli()
    {
        var kodlar = Enumerable.Range(0, 200).Select(_ => XorProtocol.NewCode()).ToList();

        Assert.All(kodlar, k => Assert.Equal(XorProtocol.CodeDigits, k.Length));
        Assert.All(kodlar, k => Assert.All(k, c => Assert.True(char.IsAsciiDigit(c))));
        Assert.True(kodlar.Distinct().Count() > 190, "Kodlar yeterince rastgele degil.");
    }
}

public class XorVerifyTests
{
    private const string Kod = "123456654321";

    [Fact]
    public void Dogru_cevap_kabul_edilir()
    {
        Assert.True(XorProtocol.Verify(Kod, XorProtocol.Solve(Kod)));
    }

    [Theory]
    [InlineData("053 0865")]
    [InlineData("0530865")]
    [InlineData(" 0530865 ")]
    [InlineData("053-0865")]
    public void Bosluk_ve_tire_gormezden_gelinir(string girilen)
    {
        // Beklenen cevap: 123456 ^ 654321 = 530865
        Assert.Equal("0530865", XorProtocol.Solve(Kod));
        Assert.True(XorProtocol.Verify(Kod, girilen));
    }

    [Theory]
    [InlineData("0530866")]   // bir hane yanlis
    [InlineData("530865")]    // basa sifir konmamis
    [InlineData("05308650")]  // fazla hane
    [InlineData("")]
    [InlineData(null)]
    public void Yanlis_cevap_reddedilir(string? girilen)
    {
        Assert.False(XorProtocol.Verify(Kod, girilen));
    }

    [Fact]
    public void Baska_kodun_cevabi_kabul_edilmez()
    {
        Assert.False(XorProtocol.Verify(Kod, XorProtocol.Solve("999999000000")));
    }

    [Fact]
    public void Ekran_bicimleri_okunakli()
    {
        Assert.Equal("1234 5665 4321", XorProtocol.FormatCode(Kod));
        Assert.Equal("053 0865", XorProtocol.FormatAnswer("0530865"));
    }
}
