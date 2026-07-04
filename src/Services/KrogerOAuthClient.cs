using System.Net.Http.Headers;
using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Models;
using KrogerShopperMcp.Utilities;
using Microsoft.Extensions.Logging;

namespace KrogerShopperMcp.Services;

internal sealed class KrogerOAuthClient
{
    private readonly KrogerConfig _config;
    private readonly IReadOnlyList<string> _defaultScopes;
    private readonly ILogger<KrogerOAuthClient> _logger;

    public KrogerOAuthClient(KrogerConfig config, IReadOnlyList<string> defaultScopes, ILogger<KrogerOAuthClient> logger)
    {
        _config = config;
        _defaultScopes = defaultScopes;
        _logger = logger;
    }

    public IReadOnlyList<string> DefaultScopes => _defaultScopes;
    public string DefaultScopeString => string.Join(' ', _defaultScopes);

    public IReadOnlyList<string> ParseScopes(string? rawScopes)
    {
        return ScopeParser.ParseScopes(rawScopes, _defaultScopes);
    }

    public string BuildAuthorizeUrl(string state, IReadOnlyList<string> scopes)
    {
        var query = new Dictionary<string, string>
        {
            ["scope"] = string.Join(' ', scopes),
            ["response_type"] = "code",
            ["client_id"] = _config.ClientId,
            ["redirect_uri"] = _config.RedirectUri,
            ["state"] = state,
            ["banner"] = _config.Banner
        };

        var builder = new UriBuilder(_config.AuthorizeUrl)
        {
            Query = string.Join("&", query.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"))
        };

        return builder.Uri.ToString();
    }

    public async Task<KrogerTokenResponse> ExchangeAuthorizationCodeAsync(
        KrogerStore store,
        string code,
        string? state,
        string scope)
    {
        _logger.LogInformation("Starting Kroger token exchange for state {State}", state ?? "(none)");
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, _config.TokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _config.GetBasicAuthToken());
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _config.RedirectUri
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Kroger token exchange failed with status {StatusCode} {ReasonPhrase} for state {State}. Body: {Body}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                state ?? "(none)",
                SummarizeBody(body));
            throw new InvalidOperationException($"token exchange failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var token = JsonDefaults.DeserializeCaseInsensitive<KrogerTokenResponse>(body)
                    ?? throw new InvalidOperationException("token response was empty");

        var resolvedScope = string.IsNullOrWhiteSpace(state)
            ? scope
            : await store.GetScopeForStateAsync(state) ?? scope;

        await store.UpsertTokenAsync(token, resolvedScope);
        _logger.LogInformation("Kroger token exchange succeeded for state {State} with scope {Scope}", state ?? "(none)", resolvedScope);
        return token;
    }

    public async Task<KrogerTokenResponse> RefreshTokenAsync(KrogerStore store)
    {
        var existing = await store.GetStoredTokenAsync();
        if (existing is null || string.IsNullOrWhiteSpace(existing.RefreshToken))
        {
            _logger.LogWarning("Kroger token refresh requested but no refresh token is stored");
            throw new InvalidOperationException("no refresh token stored");
        }

        _logger.LogInformation("Refreshing Kroger access token");
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, _config.TokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _config.GetBasicAuthToken());
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = existing.RefreshToken
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Kroger token refresh failed with status {StatusCode} {ReasonPhrase}. Body: {Body}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                SummarizeBody(body));
            throw new InvalidOperationException($"refresh failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var token = JsonDefaults.DeserializeCaseInsensitive<KrogerTokenResponse>(body)
                    ?? throw new InvalidOperationException("refresh response was empty");

        await store.UpsertTokenAsync(token, existing.Scope);
        _logger.LogInformation("Kroger token refresh succeeded; expires in {ExpiresIn} seconds", token.ExpiresIn);
        return token;
    }

    public async Task<string> GetUsableAccessTokenAsync(KrogerStore store)
    {
        var existing = await store.GetStoredTokenAsync();
        if (existing is null)
        {
            _logger.LogWarning("Kroger access token requested but none is stored");
            throw new InvalidOperationException("no stored Kroger token");
        }

        if (existing.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            _logger.LogInformation("Stored Kroger access token is expiring soon; refreshing automatically");
            var refreshed = await RefreshTokenAsync(store);
            return refreshed.AccessToken;
        }

        return existing.AccessToken;
    }

    private static string SummarizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty)";
        }

        const int maxLength = 400;
        return body.Length <= maxLength ? body : $"{body[..maxLength]}...";
    }
}
