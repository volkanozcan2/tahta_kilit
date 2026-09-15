using System.Text.Json;
using System.Text.Json.Serialization;

namespace TahtaKilit.Core;

/// <summary>
/// Eslestirme QR'inin icerigi. Kurulum sihirbazinda bir kez gosterilir,
/// ogretmenin telefonu okutup tahtayi listesine ekler.
/// </summary>
public sealed record PairingPayload(
    [property: JsonPropertyName("v")] int V,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ad")] string Ad,
    [property: JsonPropertyName("k")] string K)
{
    public const int CurrentVersion = 1;

    public static PairingPayload Create(string boardId, string boardName, byte[] key) =>
        new(CurrentVersion, boardId, boardName, Base64Url.Encode(key));

    public byte[] DecodeKey() => Base64Url.Decode(K);
}

/// <summary>
/// Kilit ekranindaki QR'in icerigi. Her kilit acma turunda yeniden uretilir.
/// Gizli bilgi tasimaz; sadece hangi tahta ve hangi cagri oldugunu soyler.
/// </summary>
public sealed record ChallengePayload(
    [property: JsonPropertyName("v")] int V,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("c")] string C)
{
    public const int CurrentVersion = 1;

    public static ChallengePayload Create(string boardId, string challenge) =>
        new(CurrentVersion, boardId, challenge);
}

/// <summary>QR iceriklerinin ortak JSON bicimi. Telefon tarafi ile birebir ayni.</summary>
public static class QrJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, Options);

    public static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

/// <summary>Anahtarlari QR'a sigacak sekilde tasimak icin base64url (dolgusuz).</summary>
public static class Base64Url
{
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }
}
