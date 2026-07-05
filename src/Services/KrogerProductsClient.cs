using System.Net.Http.Headers;
using System.Text.Json;
using System.Collections.Concurrent;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Models;
using Microsoft.Extensions.Logging;

namespace KrogerShopperMcp.Services;

internal sealed class KrogerProductsClient
{
    private const int BulkLookupConcurrency = 6;
    private static readonly TimeSpan ProductCacheTtl = TimeSpan.FromMinutes(15);
    private readonly KrogerOAuthClient _oauthClient;
    private readonly ILogger<KrogerProductsClient> _logger;
    private readonly ConcurrentDictionary<string, CachedProductSnapshot> _productCache = new(StringComparer.Ordinal);

    public KrogerProductsClient(KrogerOAuthClient oauthClient, ILogger<KrogerProductsClient> logger)
    {
        _oauthClient = oauthClient;
        _logger = logger;
    }

    public async Task<KrogerProductSnapshot?> GetProductByUpcAsync(KrogerStore store, string upc, string? locationId)
    {
        if (TryGetCachedProduct(upc, locationId, out var cachedSnapshot))
        {
            return cachedSnapshot;
        }

        var accessToken = await _oauthClient.GetUsableAccessTokenAsync(store);
        return await GetAndCacheProductByUpcWithTokenAsync(accessToken, upc, locationId);
    }

    public async Task<IReadOnlyDictionary<string, KrogerProductSnapshot?>> GetProductsByUpcsAsync(
        KrogerStore store,
        IEnumerable<string> upcs,
        string? locationId)
    {
        var normalizedUpcs = upcs
            .Where(static upc => !string.IsNullOrWhiteSpace(upc))
            .Select(static upc => upc.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedUpcs.Length == 0)
        {
            return new Dictionary<string, KrogerProductSnapshot?>(StringComparer.Ordinal);
        }

        var results = new Dictionary<string, KrogerProductSnapshot?>(StringComparer.Ordinal);
        var missingUpcs = new List<string>();
        foreach (var upc in normalizedUpcs)
        {
            if (TryGetCachedProduct(upc, locationId, out var cachedSnapshot))
            {
                results[upc] = cachedSnapshot;
            }
            else
            {
                missingUpcs.Add(upc);
            }
        }

        if (missingUpcs.Count == 0)
        {
            return results;
        }

        var accessToken = await _oauthClient.GetUsableAccessTokenAsync(store);
        var semaphore = new SemaphoreSlim(BulkLookupConcurrency);
        var tasks = missingUpcs.Select(async upc =>
        {
            await semaphore.WaitAsync();
            try
            {
                var snapshot = await GetAndCacheProductByUpcWithTokenAsync(accessToken, upc, locationId);
                return (upc, snapshot);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var fetchedResults = await Task.WhenAll(tasks);
        foreach (var (upc, snapshot) in fetchedResults)
        {
            results[upc] = snapshot;
        }

        return results;
    }

    private async Task<KrogerProductSnapshot?> GetAndCacheProductByUpcWithTokenAsync(string accessToken, string upc, string? locationId)
    {
        var normalizedUpc = upc.Trim();
        var cacheKey = BuildCacheKey(normalizedUpc, locationId);
        var parameters = new List<string>
        {
            $"filter.term={Uri.EscapeDataString(normalizedUpc)}",
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
            _logger.LogWarning(
                "Kroger product lookup failed for upc {Upc} at location {LocationId} with status {StatusCode} {ReasonPhrase}. Body: {Body}",
                normalizedUpc,
                locationId ?? "(default)",
                (int)response.StatusCode,
                response.ReasonPhrase,
                SummarizeBody(body));
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        KrogerProductSnapshot? matchedSnapshot = null;
        foreach (var product in data.EnumerateArray())
        {
            var snapshot = ParseProductSnapshot(product);
            if (snapshot is not null && string.Equals(snapshot.Upc, normalizedUpc, StringComparison.Ordinal))
            {
                matchedSnapshot = snapshot;
                break;
            }
        }

        _productCache[cacheKey] = new CachedProductSnapshot(matchedSnapshot, DateTimeOffset.UtcNow.Add(ProductCacheTtl));
        return matchedSnapshot;
    }

    private bool TryGetCachedProduct(string upc, string? locationId, out KrogerProductSnapshot? snapshot)
    {
        var cacheKey = BuildCacheKey(upc.Trim(), locationId);
        if (_productCache.TryGetValue(cacheKey, out var cached))
        {
            if (cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                snapshot = cached.Snapshot;
                return true;
            }

            _productCache.TryRemove(cacheKey, out _);
        }

        snapshot = null;
        return false;
    }

    private static string BuildCacheKey(string upc, string? locationId)
        => $"{(string.IsNullOrWhiteSpace(locationId) ? "(default)" : locationId.Trim())}::{upc}";

    private sealed record CachedProductSnapshot(KrogerProductSnapshot? Snapshot, DateTimeOffset ExpiresAtUtc);

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
            _logger.LogWarning(
                "Kroger product search failed for query {Query} at location {LocationId} with status {StatusCode} {ReasonPhrase}. Body: {Body}",
                query,
                locationId ?? "(default)",
                (int)response.StatusCode,
                response.ReasonPhrase,
                SummarizeBody(body));
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
                    product_page_uri = snapshot.ProductPageUri,
                    image_url = snapshot.ImageUrl,
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

    private static string SummarizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty)";
        }

        const int maxLength = 400;
        return body.Length <= maxLength ? body : $"{body[..maxLength]}...";
    }

    private static KrogerProductSnapshot? ParseProductSnapshot(JsonElement product)
    {
        var productId = GetStringValue(product, "productId");
        var upc = GetStringValue(product, "upc");
        if (string.IsNullOrWhiteSpace(upc))
        {
            return null;
        }

        var productPageUri = GetStringValue(product, "productPageURI");
        var description = GetStringValue(product, "description");
        var brand = GetStringValue(product, "brand");
        string? imageUrl = null;
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
            size = GetStringValue(firstItem, "size");
            soldBy = GetStringValue(firstItem, "soldBy");

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
                stockLevel = GetStringValue(inventory, "stockLevel");
            }

            if (firstItem.TryGetProperty("fulfillment", out var fulfillment) && fulfillment.ValueKind == JsonValueKind.Object)
            {
                curbside = GetBoolean(fulfillment, "curbside");
                delivery = GetBoolean(fulfillment, "delivery");
                inStore = GetBoolean(fulfillment, "inStore");
                shipToHome = GetBoolean(fulfillment, "shipToHome");
            }
        }

        if (product.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            imageUrl = GetPreferredImageUrl(images);
        }

        return new KrogerProductSnapshot(
            productId,
            upc,
            productPageUri,
            imageUrl,
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

    private static string? GetPreferredImageUrl(JsonElement images)
    {
        foreach (var image in images.EnumerateArray())
        {
            if (image.TryGetProperty("featured", out var featured) &&
                featured.ValueKind == JsonValueKind.True &&
                TryGetImageUrl(image, out var featuredUrl))
            {
                return featuredUrl;
            }
        }

        foreach (var image in images.EnumerateArray())
        {
            if (TryGetImageUrl(image, out var imageUrl))
            {
                return imageUrl;
            }
        }

        return null;
    }

    private static bool TryGetImageUrl(JsonElement image, out string? imageUrl)
    {
        imageUrl = null;
        if (!image.TryGetProperty("sizes", out var sizes) || sizes.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var preferredSize in new[] { "medium", "large", "xlarge", "small", "thumbnail" })
        {
            foreach (var size in sizes.EnumerateArray())
            {
                if (GetStringValue(size, "size") == preferredSize)
                {
                    imageUrl = GetStringValue(size, "url");
                    return !string.IsNullOrWhiteSpace(imageUrl);
                }
            }
        }

        return false;
    }

    private static string? GetStringValue(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? GetBoolean(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }
}
