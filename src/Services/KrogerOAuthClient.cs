using System.Net.Http.Headers;
using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Models;
using KrogerShopperMcp.Utilities;

namespace KrogerShopperMcp.Services;

internal sealed class KrogerOAuthClient
{
    private readonly KrogerConfig _config;
    private readonly IReadOnlyList<string> _defaultScopes;

    public KrogerOAuthClient(KrogerConfig config, IReadOnlyList<string> defaultScopes)
    {
        _config = config;
        _defaultScopes = defaultScopes;
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
            throw new InvalidOperationException($"token exchange failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var token = JsonDefaults.DeserializeCaseInsensitive<KrogerTokenResponse>(body)
                    ?? throw new InvalidOperationException("token response was empty");

        var resolvedScope = string.IsNullOrWhiteSpace(state)
            ? scope
            : await store.GetScopeForStateAsync(state) ?? scope;

        await store.UpsertTokenAsync(token, resolvedScope);
        return token;
    }

    public async Task<KrogerTokenResponse> RefreshTokenAsync(KrogerStore store)
    {
        var existing = await store.GetStoredTokenAsync();
        if (existing is null || string.IsNullOrWhiteSpace(existing.RefreshToken))
        {
            throw new InvalidOperationException("no refresh token stored");
        }

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
            throw new InvalidOperationException($"refresh failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var token = JsonDefaults.DeserializeCaseInsensitive<KrogerTokenResponse>(body)
                    ?? throw new InvalidOperationException("refresh response was empty");

        await store.UpsertTokenAsync(token, existing.Scope);
        return token;
    }

    public async Task<string> GetUsableAccessTokenAsync(KrogerStore store)
    {
        var existing = await store.GetStoredTokenAsync();
        if (existing is null)
        {
            throw new InvalidOperationException("no stored Kroger token");
        }

        if (existing.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            var refreshed = await RefreshTokenAsync(store);
            return refreshed.AccessToken;
        }

        return existing.AccessToken;
    }
}
