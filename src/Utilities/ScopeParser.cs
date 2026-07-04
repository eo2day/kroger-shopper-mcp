namespace KrogerShopperMcp.Utilities;

internal static class ScopeParser
{
    public static IReadOnlyList<string> ParseScopes(string? rawScopes, IReadOnlyList<string> fallbackScopes)
    {
        if (string.IsNullOrWhiteSpace(rawScopes))
        {
            return fallbackScopes;
        }

        return rawScopes
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
