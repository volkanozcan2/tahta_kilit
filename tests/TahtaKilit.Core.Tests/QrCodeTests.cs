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
        var icerik = QrJson.Serialize(ChallengePayload.Create("ABCDEFGH", "4F7K2Q"));

        var okunan = Oku(QrCode.CreateMatrix(icerik));

        Assert.Equal(icerik, okunan);
    }

    [Fact]
    public void Okunan_karekod_telefonun_bekledigi_bicimde()
    {
        // Telefon tarafi (pwa/core.js) bu alanlari ariyor.
        var icerik = QrJson.Serialize(ChallengePayload.Create("TK3M9WP2", "H8N4TV"));

        var payload = QrJson.Deserialize<ChallengePayload>(Oku(QrCode.CreateMatrix(icerik)));

        Assert.NotNull(payload);
        Assert.Equal(1, payload.V);
        Assert.Equal("TK3M9WP2", payload.Id);
        Assert.Equal("H8N4TV", payload.C);
    }

    [Fact]
    public void Eslestirme_karekodu_anahtari_eksiksiz_tasiyor()
    {
        // Eslestirme karekodu 32 baytlik anahtari tasir; en buyuk icerik budur.
        var key = UnlockProtocol.NewKey();
        var icerik = QrJson.Serialize(
            PairingPayload.Create(UnlockProtocol.NewBoardId(), "Z-Blok 204 Akilli Tahta", key));

        var payload = QrJson.Deserialize<PairingPayload>(Oku(QrCode.CreateMatrix(icerik)));

        Assert.NotNull(payload);
        Assert.Equal(key, payload.DecodeKey());
    }

    [Fact]
    public void Png_ciktisi_gecerli_bir_png()
    {
        var png = QrCode.CreatePng(QrJson.Serialize(ChallengePayload.Create("ABCDEFGH", "4F7K2Q")));

        Assert.True(png.Length > 100);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]); // PNG imzasi
    }

    [Fact]
    public void Bos_icerik_reddedilir()
    {
        Assert.Throws<ArgumentException>(() => QrCode.CreateMatrix(""));
    }
}
