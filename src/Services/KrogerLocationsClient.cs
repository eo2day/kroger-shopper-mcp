using System.Net.Http.Headers;
using System.Text.Json;
using KrogerShopperMcp.Infrastructure;

namespace KrogerShopperMcp.Services;

internal sealed class KrogerLocationsClient
{
    private readonly KrogerOAuthClient _oauthClient;

    public KrogerLocationsClient(KrogerOAuthClient oauthClient)
    {
        _oauthClient = oauthClient;
    }

    public async Task<object> SearchLocationsAsync(KrogerStore store, string zipCode, string? chain, int limit)
    {
        var accessToken = await _oauthClient.GetUsableAccessTokenAsync(store);
        var parameters = new List<string>
        {
            $"filter.zipCode.near={Uri.EscapeDataString(zipCode)}",
            $"filter.limit={limit}"
        };

        if (!string.IsNullOrWhiteSpace(chain))
        {
            parameters.Add($"filter.chain={Uri.EscapeDataString(chain)}");
        }

        var url = $"https://api.kroger.com/v1/locations?{string.Join("&", parameters)}";

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new
            {
                ok = false,
                status = (int)response.StatusCode,
                reason = response.ReasonPhrase,
                raw = body
            };
        }

        using var doc = JsonDocument.Parse(body);
        var items = new List<object>();

        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var location in data.EnumerateArray())
            {
                static string? GetString(JsonElement element, string name) =>
                    element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                        ? value.GetString()
                        : null;

                var address = location.TryGetProperty("address", out var addressEl) && addressEl.ValueKind == JsonValueKind.Object
                    ? new
                    {
                        address_line_1 = GetString(addressEl, "addressLine1"),
                        city = GetString(addressEl, "city"),
                        state = GetString(addressEl, "state"),
                        zip_code = GetString(addressEl, "zipCode")
                    }
                    : null;

                items.Add(new
                {
                    location_id = GetString(location, "locationId"),
                    chain = GetString(location, "chain"),
                    name = GetString(location, "name"),
                    phone = GetString(location, "phone"),
                    address
                });
            }
        }

        return new
        {
            ok = true,
            zip_code = zipCode,
            chain,
            limit,
            count = items.Count,
            items
        };
    }
}
