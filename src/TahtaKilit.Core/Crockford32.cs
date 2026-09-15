namespace TahtaKilit.Core;

/// <summary>
/// Crockford Base32: ekrandan okunup elle yazilacak kodlar icin.
/// I/L/O harfleri ile 1/0 rakamlari karistirilamaz, U harfi hic kullanilmaz.
/// </summary>
public static class Crockford32
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Verilen sayiyi <paramref name="length"/> karakterlik koda cevirir.</summary>
    public static string Encode(ulong value, int length)
    {
        if (length is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(length));

        var chars = new char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }

        return new string(chars);
    }

    /// <summary>
    /// Kullanicinin yazdigi kodu tekillestirir: kucuk harfler buyutulur,
    /// bosluk ve tire atilir, karistirilan harfler duzeltilir (I/L to 1, O to 0).
    /// Gecersiz karakter varsa <c>false</c> doner.
    /// </summary>
    public static bool TryNormalize(string? input, int expectedLength, out string normalized)
    {
        normalized = string.Empty;
        if (input is null)
            return false;

        var buffer = new char[expectedLength];
        var count = 0;

        foreach (var raw in input)
        {
            if (raw is ' ' or '-' or '\t' or '_')
                continue;

            var c = char.ToUpperInvariant(raw);
            c = c switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                _ => c,
            };

            if (Alphabet.IndexOf(c) < 0)
                return false;

            if (count == expectedLength)
                return false; // beklenenden uzun

            buffer[count++] = c;
        }

        if (count != expectedLength)
            return false;

        normalized = new string(buffer);
        return true;
    }
}
