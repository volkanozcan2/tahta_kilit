using QRCoder;

namespace TahtaKilit.Core;

/// <summary>
/// Karekod uretimi. QRCoder'in yalnizca platformdan bagimsiz parcalari
/// kullanilir (System.Drawing yok), boylece Core her yerde derlenir ve
/// uretilen karekod testlerde geri okunarak dogrulanabilir.
/// </summary>
public static class QrCode
{
    /// <summary>
    /// Hata duzeltme seviyesi. Tahta ekrani parlama ve parmak izi yuzunden
    /// ideal olmadigi icin orta seviye (yaklasik %15 kurtarma) secildi.
    /// </summary>
    private const QRCodeGenerator.ECCLevel Ecc = QRCodeGenerator.ECCLevel.M;

    /// <summary>Karekodu modul matrisi olarak uretir (<c>true</c> = koyu).</summary>
    public static bool[,] CreateMatrix(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, Ecc);

        var modules = data.ModuleMatrix;
        var size = modules.Count;
        var matrix = new bool[size, size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
                matrix[y, x] = modules[y][x];
        }

        return matrix;
    }

    /// <summary>
    /// Karekodu PNG olarak uretir. Kilit ekrani bu baytlari dogrudan gosterir.
    /// </summary>
    /// <param name="pixelsPerModule">Her modulun kac piksel olacagi.</param>
    public static byte[] CreatePng(string text, int pixelsPerModule = 10)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelsPerModule, 1);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, Ecc);

        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }
}
