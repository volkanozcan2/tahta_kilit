using System.Security.Cryptography;

namespace TahtaKilit.Core;

/// <summary>
/// Kilit acma kurali.
///
/// Tahta 12 haneli bir sayi uretip karekod olarak gosterir. Kilidi acan sayi,
/// bu sayinin ilk 6 hanesi ile son 6 hanesinin bit duzeyinde XOR'udur.
/// Telefon uygulamasi karekodu okuyup bu islemi yapar.
///
/// DIKKAT — bu kuralda gizli anahtar yoktur. Kilidin cevabi, kilidin uzerinde
/// gosterilen sayidan hesaplanir; kurali bilen herkes ayni sonucu bulabilir.
/// Yani bu bir guvenlik onlemi degil, yanlislikla veya dusuncesizce kullanimi
/// caydiran bir kapidir. Bilerek boyle secildi (bkz. docs/TASARIM.md).
/// </summary>
public static class XorProtocol
{
    /// <summary>Karekodda gosterilen sayinin hane sayisi.</summary>
    public const int CodeDigits = 12;

    /// <summary>Yarim uzunluk; sayi ikiye bolunur.</summary>
    public const int HalfDigits = CodeDigits / 2;

    /// <summary>
    /// Cevabin hane sayisi. Iki alti haneli sayinin XOR'u en fazla
    /// 1048575 olabilir, yani yedi hane. Kisa sonuclar basa sifir konarak
    /// bu uzunluga tamamlanir, boylece ekranda hep ayni genislikte gorunur.
    /// </summary>
    public const int AnswerDigits = 7;

    /// <summary>Karekodun ne kadar sure sonra yenilendigi.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(30);

    /// <summary>Yeni bir 12 haneli kod uretir. Basta sifir olabilir.</summary>
    public static string NewCode()
    {
        var digits = new char[CodeDigits];
        for (var i = 0; i < CodeDigits; i++)
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));

        return new string(digits);
    }

    /// <summary>
    /// Koddan cevabi hesaplar: ilk yarim XOR son yarim.
    /// Hem tahta hem telefon ayni fonksiyonu kullanir.
    /// </summary>
    public static string Solve(string code)
    {
        if (!TryNormalizeCode(code, out var normalized))
            throw new ArgumentException($"Kod {CodeDigits} haneli olmali.", nameof(code));

        var ilk = int.Parse(normalized[..HalfDigits]);
        var son = int.Parse(normalized[HalfDigits..]);

        return (ilk ^ son).ToString($"D{AnswerDigits}");
    }

    /// <summary>Girilen cevabin dogru olup olmadigini soyler.</summary>
    public static bool Verify(string code, string? entered) =>
        TryNormalizeAnswer(entered, out var normalized) && Solve(code) == normalized;

    /// <summary>
    /// Karekodda gosterilen sayiyi tekillestirir; bosluk ve tireler atilir,
    /// sonuc tam <see cref="CodeDigits"/> haneli rakam dizisi olmalidir.
    /// </summary>
    public static bool TryNormalizeCode(string? code, out string normalized) =>
        TryNormalizeDigits(code, CodeDigits, out normalized);

    /// <summary>Girilen cevabi tekillestirir.</summary>
    public static bool TryNormalizeAnswer(string? entered, out string normalized) =>
        TryNormalizeDigits(entered, AnswerDigits, out normalized);

    private static bool TryNormalizeDigits(string? input, int expectedLength, out string normalized)
    {
        normalized = string.Empty;
        if (input is null)
            return false;

        Span<char> buffer = stackalloc char[expectedLength];
        var count = 0;

        foreach (var c in input)
        {
            if (c is ' ' or '-' or '\t')
                continue;

            if (!char.IsAsciiDigit(c) || count == expectedLength)
                return false;

            buffer[count++] = c;
        }

        if (count != expectedLength)
            return false;

        normalized = new string(buffer);
        return true;
    }

    /// <summary>Ekranda okunakli gosterim: <c>1234 5678 9012</c>.</summary>
    public static string FormatCode(string code) =>
        string.Join(' ', Enumerable.Range(0, CodeDigits / 4).Select(i => code.Substring(i * 4, 4)));

    /// <summary>Cevabi okunakli gosterir: <c>104 8575</c>.</summary>
    public static string FormatAnswer(string answer) =>
        answer.Length == AnswerDigits ? answer[..3] + " " + answer[3..] : answer;
}
