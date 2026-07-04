using KrogerShopperMcp.Infrastructure;

namespace KrogerShopperMcp.Configuration;

internal sealed class KrogerConfig
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string RedirectUri { get; init; }
    public required string Banner { get; init; }
    public required string TokenUrl { get; init; }
    public required string AuthorizeUrl { get; init; }
    public required string DbPath { get; init; }
    public required string ServiceUrl { get; init; }
    public required string PublicBaseUrl { get; init; }

    public static KrogerConfig LoadFromEnvironment()
    {
        EnvFileLoader.TryLoad();

        return new KrogerConfig
        {
            ClientId = GetRequired("KROGER_CLIENT_ID"),
            ClientSecret = GetRequired("KROGER_CLIENT_SECRET"),
            RedirectUri = GetRequired("KROGER_REDIRECT_URI"),
            Banner = GetRequired("KROGER_BANNER"),
            TokenUrl = GetRequired("KROGER_TOKEN_URL"),
            AuthorizeUrl = GetRequired("KROGER_AUTHORIZE_URL"),
            DbPath = GetRequired("KROGER_DB_PATH"),
            ServiceUrl = GetOptional("KROGER_SERVICE_URL", "http://127.0.0.1:5092"),
            PublicBaseUrl = GetOptional("KROGER_PUBLIC_BASE_URL", "http://127.0.0.1:5092")
        };
    }

    public string GetBasicAuthToken()
    {
        return Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{ClientId}:{ClientSecret}"));
    }

    private static string GetRequired(string key)
    {
        return Environment.GetEnvironmentVariable(key) switch
        {
            { Length: > 0 } value => value,
            _ => throw new InvalidOperationException($"missing env var {key}")
        };
    }

    private static string GetOptional(string key, string fallback)
    {
        return Environment.GetEnvironmentVariable(key) switch
        {
            { Length: > 0 } value => value,
            _ => fallback
        };
    }
}
