using System.Net.Http.Headers;
using System.Text.Json;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Models;

namespace KrogerShopperMcp.Services;

internal sealed class KrogerProductsClient
{
    private readonly KrogerOAuthClient _oauthClient;

    public KrogerProductsClient(KrogerOAuthClient oauthClient)
    {
        _oauthClient = oauthClient;
    }

    public async Task<KrogerProductSnapshot?> GetProductByUpcAsync(KrogerStore store, string upc, string? locationId)
    {
        var accessToken = await _oauthClient.GetUsableAccessTokenAsync(store);
        var parameters = new List<string>
        {
            $"filter.term={Uri.EscapeDataString(upc)}",
            "filter.limit=10"
        };

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            parameters.Add($"filter.locationId={Uri.EscapeDataString(locationId)}");
        }

        var url = $"https://api.kroger.com/v1/products?{string.Join("&", parameters)}";

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var product in data.EnumerateArray())
        {
            var snapshot = ParseProductSnapshot(product);
            if (snapshot is not null && string.Equals(snapshot.Upc, upc, StringComparison.Ordinal))
            {
                return snapshot;
            }
        }

        return null;
    }

    public async Task<object> SearchProductsAsync(KrogerStore store, string query, int limit, string? locationId)
    {
        var accessToken = await _oauthClient.GetUsableAccessTokenAsync(store);
        var parameters = new List<string>
        {
            $"filter.term={Uri.EscapeDataString(query)}",
            $"filter.limit={limit}"
        };

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            parameters.Add($"filter.locationId={Uri.EscapeDataString(locationId)}");
        }

        var url = $"https://api.kroger.com/v1/products?{string.Join("&", parameters)}";

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
            foreach (var product in data.EnumerateArray())
            {
                var snapshot = ParseProductSnapshot(product);
                if (snapshot is null)
                {
                    continue;
                }

                items.Add(new
                {
                    product_id = snapshot.ProductId,
                    upc = snapshot.Upc,
                    description = snapshot.Description,
                    brand = snapshot.Brand,
                    size = snapshot.Size,
                    sold_by = snapshot.SoldBy,
                    regular_price = snapshot.RegularPrice,
                    promo_price = snapshot.PromoPrice,
                    stock_level = snapshot.StockLevel,
                    fulfillment = new
                    {
                        curbside = snapshot.Curbside,
                        delivery = snapshot.Delivery,
                        in_store = snapshot.InStore,
                        ship_to_home = snapshot.ShipToHome
                    }
                });
            }
        }

        return new
        {
            ok = true,
            query,
            limit,
            location_id = string.IsNullOrWhiteSpace(locationId) ? null : locationId,
            count = items.Count,
            items
        };
    }

    private static KrogerProductSnapshot? ParseProductSnapshot(JsonElement product)
    {
        static string? GetString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        var productId = GetString(product, "productId");
        var upc = GetString(product, "upc");
        if (string.IsNullOrWhiteSpace(upc))
        {
            return null;
        }

        var description = GetString(product, "description");
        var brand = GetString(product, "brand");
        string? size = null;
        string? soldBy = null;
        decimal? regularPrice = null;
        decimal? promoPrice = null;
        string? stockLevel = null;
        bool? curbside = null;
        bool? delivery = null;
        bool? inStore = null;
        bool? shipToHome = null;

        if (product.TryGetProperty("items", out var subItems) &&
            subItems.ValueKind == JsonValueKind.Array &&
            subItems.GetArrayLength() > 0)
        {
            var firstItem = subItems[0];
            size = GetString(firstItem, "size");
            soldBy = GetString(firstItem, "soldBy");

            if (firstItem.TryGetProperty("price", out var price) && price.ValueKind == JsonValueKind.Object)
            {
                if (price.TryGetProperty("regular", out var regular) && regular.ValueKind == JsonValueKind.Number)
                {
                    regularPrice = regular.GetDecimal();
                }

                if (price.TryGetProperty("promo", out var promo) && promo.ValueKind == JsonValueKind.Number)
                {
                    promoPrice = promo.GetDecimal();
                }
            }

            if (firstItem.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Object)
            {
                stockLevel = GetString(inventory, "stockLevel");
            }

            if (firstItem.TryGetProperty("fulfillment", out var fulfillment) && fulfillment.ValueKind == JsonValueKind.Object)
            {
                curbside = GetBoolean(fulfillment, "curbside");
                delivery = GetBoolean(fulfillment, "delivery");
                inStore = GetBoolean(fulfillment, "inStore");
                shipToHome = GetBoolean(fulfillment, "shipToHome");
            }
        }

        return new KrogerProductSnapshot(
            productId,
            upc,
            description,
            brand,
            size,
            soldBy,
            regularPrice,
            promoPrice,
            stockLevel,
            curbside,
            delivery,
            inStore,
            shipToHome);
    }

    private static bool? GetBoolean(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }
}
