using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TahtaKilit.Core;

/// <summary>
/// Tahta ile telefon arasindaki cagri-cevap protokolu.
///
/// Tahta rastgele bir <em>cagri</em> uretir (6 karakter), ekranda QR ve yazi
/// olarak gosterir. Telefon o tahtanin gizli anahtari ile cagriyi imzalayip
/// 8 haneli <em>cevabi</em> uretir. Tahta ayni hesabi yapip karsilastirir.
///
/// Ne saat ne de ag kullanilir; tahtanin saati kaymis veya internet kopuk
/// olsa da calisir. Her kilit acmada cagri degistigi icin gorulen bir kod
/// ikinci kez ise yaramaz.
/// </summary>
public static class UnlockProtocol
{
    /// <summary>Protokol surumu; imzalanan metne girer, ileride degisirse eski kodlar gecersizlesir.</summary>
    public const string Version = "TK1";

    /// <summary>Gizli anahtarin bayt uzunlugu (256 bit).</summary>
    public const int KeyLength = 32;

    /// <summary>Cagri kodunun karakter sayisi (30 bit).</summary>
    public const int ChallengeLength = 6;

    /// <summary>Cevabin hane sayisi.</summary>
    public const int ResponseDigits = 8;

    /// <summary>Tahta kimliginin karakter sayisi (40 bit).</summary>
    public const int BoardIdLength = 8;

    private const uint ChallengeMask = (1u << 30) - 1;
    private const int ResponseModulus = 100_000_000; // 10^8

    /// <summary>Yeni bir tahta icin 256 bitlik gizli anahtar uretir.</summary>
    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeyLength);

    /// <summary>Yeni bir tahta kimligi uretir (QR icinde tasinir, elle yazilmaz).</summary>
    public static string NewBoardId()
    {
        // 8 Crockford karakteri = 40 bit.
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes[3..]);
        return Crockford32.Encode(BinaryPrimitives.ReadUInt64BigEndian(bytes), BoardIdLength);
    }

    /// <summary>
    /// Yeni bir cagri uretir. Her kilit acma denemesi turu icin bir kez cagrilir.
    /// </summary>
    public static string NewChallenge() =>
        Crockford32.Encode(
            BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)) & ChallengeMask,
            ChallengeLength);

    /// <summary>
    /// Cagriya karsilik gelen 8 haneli cevabi hesaplar. Hem telefon hem tahta
    /// tarafinda ayni fonksiyon kullanilir.
    /// </summary>
    /// <param name="key">Tahtanin gizli anahtari (32 bayt).</param>
    /// <param name="boardId">Tahta kimligi; cevabi o tahtaya baglar.</param>
    /// <param name="challenge">Tahtanin gosterdigi 6 karakterlik cagri.</param>
    public static string ComputeResponse(byte[] key, string boardId, string challenge)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeyLength)
            throw new ArgumentException($"Anahtar {KeyLength} bayt olmali.", nameof(key));

        if (!Crockford32.TryNormalize(boardId, BoardIdLength, out var normalizedBoardId))
            throw new ArgumentException("Gecersiz tahta kimligi.", nameof(boardId));

        if (!Crockford32.TryNormalize(challenge, ChallengeLength, out var normalizedChallenge))
            throw new ArgumentException("Gecersiz cagri kodu.", nameof(challenge));

        var message = Encoding.ASCII.GetBytes(
            $"{Version}:{normalizedBoardId}:{normalizedChallenge}");

        var mac = HMACSHA256.HashData(key, message);

        // RFC 4226'daki dinamik kesme: MAC'in son yarim baytiyla secilen konumdan
        // 31 bitlik sayi alinir, sonra 10^8'e gore moda indirilir.
        var offset = mac[^1] & 0x0F;
        var truncated =
            ((uint)(mac[offset] & 0x7F) << 24) |
            ((uint)mac[offset + 1] << 16) |
            ((uint)mac[offset + 2] << 8) |
            mac[offset + 3];

        return (truncated % ResponseModulus).ToString($"D{ResponseDigits}");
    }

    /// <summary>
    /// Kullanicinin girdigi cevabi dogrular. Karsilastirma sabit zamanlidir;
    /// dogru haneler tek tek tahmin edilemez.
    /// </summary>
    public static bool VerifyResponse(byte[] key, string boardId, string challenge, string? entered)
    {
        if (!TryNormalizeResponse(entered, out var normalized))
            return false;

        var expected = ComputeResponse(key, boardId, challenge);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(normalized));
    }

    /// <summary>
    /// Girilen cevabi tekillestirir: bosluk ve tireler atilir, sonuc tam
    /// <see cref="ResponseDigits"/> haneli rakam dizisi olmalidir.
    /// </summary>
    public static bool TryNormalizeResponse(string? entered, out string normalized)
    {
        normalized = string.Empty;
        if (entered is null)
            return false;

        Span<char> buffer = stackalloc char[ResponseDigits];
        var count = 0;

        foreach (var c in entered)
        {
            if (c is ' ' or '-' or '\t')
                continue;

            if (!char.IsAsciiDigit(c) || count == ResponseDigits)
                return false;

            buffer[count++] = c;
        }

        if (count != ResponseDigits)
            return false;

        normalized = new string(buffer);
        return true;
    }

    /// <summary>Cevabi ekranda okunakli gosterir: <c>40 82 17 55</c>.</summary>
    public static string FormatForDisplay(string response) =>
        string.Join(' ', Enumerable.Range(0, response.Length / 2)
            .Select(i => response.Substring(i * 2, 2)));
}
