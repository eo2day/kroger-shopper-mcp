using System.Text.Json;

namespace KrogerShopperMcp.Utilities;

internal static class JsonDefaults
{
    private static readonly JsonSerializerOptions SnakeCaseIndented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string SerializeIndented<T>(T value)
    {
        return JsonSerializer.Serialize(value, SnakeCaseIndented);
    }

    public static T? DeserializeCaseInsensitive<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, CaseInsensitive);
    }
}
