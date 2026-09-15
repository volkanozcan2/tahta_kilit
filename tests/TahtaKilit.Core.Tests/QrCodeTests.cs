using TahtaKilit.Core;
using Xunit;
using ZXing;
using ZXing.Common;

namespace TahtaKilit.Core.Tests;

/// <summary>
/// Urettigimiz karekodu bagimsiz bir okuyucuyla (ZXing) geri okur.
/// Telefonun kilit ekranindaki karekodu gercekten okuyabilecegini dogrular.
/// </summary>
public class QrCodeTests
{
    /// <summary>
    /// Matrisi gri tonlu bir goruntuye buyutup okuyucuya verir — telefonun
    /// kameradan aldigi goruntuye yakin bir yol izlenir (sessiz bolge dahil).
    /// </summary>
    private static string Oku(bool[,] matrix, int olcek = 4, int kenar = 4)
    {
        var modul = matrix.GetLength(0);
        var boyut = (modul + kenar * 2) * olcek;
        var piksel = new byte[boyut * boyut];
        Array.Fill(piksel, (byte)255); // beyaz zemin

        for (var y = 0; y < modul; y++)
        {
            for (var x = 0; x < modul; x++)
            {
                if (!matrix[y, x])
                    continue;

                for (var dy = 0; dy < olcek; dy++)
                {
                    var satir = ((y + kenar) * olcek + dy) * boyut + (x + kenar) * olcek;
                    Array.Fill(piksel, (byte)0, satir, olcek);
                }
            }
        }

        var kaynak = new RGBLuminanceSource(
            piksel, boyut, boyut, RGBLuminanceSource.BitmapFormat.Gray8);

        var sonuc = new ZXing.QrCode.QRCodeReader()
            .decode(new BinaryBitmap(new HybridBinarizer(kaynak)));

        return sonuc?.Text ?? throw new InvalidOperationException("Karekod okunamadi.");
    }

    [Fact]
    public void Kilit_ekrani_karekodu_geri_okunabiliyor()
    {
        var kod = XorProtocol.NewCode();

        Assert.Equal(kod, Oku(QrCode.CreateMatrix(kod)));
    }

    [Theory]
    [InlineData("000000000000")]
    [InlineData("999999999999")]
    [InlineData("123456654321")]
    public void Her_kod_eksiksiz_okunuyor(string kod)
    {
        // Basta sifir olan kodlar da bozulmadan gecmeli.
        Assert.Equal(kod, Oku(QrCode.CreateMatrix(kod)));
    }

    [Fact]
    public void Okunan_karekoddan_cevap_hesaplanabiliyor()
    {
        var kod = XorProtocol.NewCode();

        var okunan = Oku(QrCode.CreateMatrix(kod));

        Assert.Equal(XorProtocol.Solve(kod), XorProtocol.Solve(okunan));
    }

    [Fact]
    public void Png_ciktisi_gecerli_bir_png()
    {
        var png = QrCode.CreatePng(XorProtocol.NewCode());

        Assert.True(png.Length > 100);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]); // PNG imzasi
    }

    [Fact]
    public void Bos_icerik_reddedilir()
    {
        Assert.Throws<ArgumentException>(() => QrCode.CreateMatrix(""));
    }
}
