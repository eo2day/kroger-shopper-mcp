using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Models;
using Microsoft.Extensions.Logging;

namespace KrogerShopperMcp.Services;

internal sealed class KrogerCartService
{
    private sealed record SavedCartItemPayload(string? Upc, int Quantity);
    private static readonly JsonSerializerOptions SavedCartJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly KrogerProductsClient _productsClient;
    private readonly KrogerOAuthClient _oauthClient;
    private readonly ILogger<KrogerCartService> _logger;

    public KrogerCartService(
        KrogerProductsClient productsClient,
        KrogerOAuthClient oauthClient,
        ILogger<KrogerCartService> logger)
    {
        _productsClient = productsClient;
        _oauthClient = oauthClient;
        _logger = logger;
    }

    public async Task<object> GetCartInfoAsync(KrogerStore store, string? locationId)
    {
        var trackedItems = await store.GetTrackedCartItemsAsync();
        var resolvedLocationId = string.IsNullOrWhiteSpace(locationId)
            ? await store.GetDefaultStoreIdAsync()
            : locationId;

        var items = new List<object>();
        foreach (var trackedItem in trackedItems)
        {
            var snapshot = await _productsClient.GetProductByUpcAsync(store, trackedItem.Upc, resolvedLocationId);
            items.Add(new
            {
                upc = trackedItem.Upc,
                tracked_quantity = trackedItem.Quantity,
                updated_at_utc = trackedItem.UpdatedAtUtc.ToString("O"),
                product_id = snapshot?.ProductId,
                description = snapshot?.Description,
                brand = snapshot?.Brand,
                size = snapshot?.Size,
                regular_price = snapshot?.RegularPrice,
                promo_price = snapshot?.PromoPrice,
                stock_level = snapshot?.StockLevel,
                in_stock = IsInStock(snapshot?.StockLevel),
                fulfillment = new
                {
                    curbside = snapshot?.Curbside,
                    delivery = snapshot?.Delivery,
                    in_store = snapshot?.InStore,
                    ship_to_home = snapshot?.ShipToHome
                }
            });
        }

        return new
        {
            ok = true,
            location_id = resolvedLocationId,
            count = items.Count,
            items
        };
    }

    public async Task<object> GetStagedCartInfoAsync(KrogerStore store, string? locationId)
    {
        var stagedItems = await store.GetStagedCartItemsAsync();
        var resolvedLocationId = string.IsNullOrWhiteSpace(locationId)
            ? await store.GetDefaultStoreIdAsync()
            : locationId;

        var items = new List<object>();
        foreach (var stagedItem in stagedItems)
        {
            var snapshot = await _productsClient.GetProductByUpcAsync(store, stagedItem.Upc, resolvedLocationId);
            items.Add(new
            {
                upc = stagedItem.Upc,
                staged_quantity = stagedItem.Quantity,
                updated_at_utc = stagedItem.UpdatedAtUtc.ToString("O"),
                product_id = snapshot?.ProductId,
                description = snapshot?.Description,
                brand = snapshot?.Brand,
                size = snapshot?.Size,
                regular_price = snapshot?.RegularPrice,
                promo_price = snapshot?.PromoPrice,
                stock_level = snapshot?.StockLevel,
                in_stock = IsInStock(snapshot?.StockLevel),
                fulfillment = new
                {
                    curbside = snapshot?.Curbside,
                    delivery = snapshot?.Delivery,
                    in_store = snapshot?.InStore,
                    ship_to_home = snapshot?.ShipToHome
                }
            });
        }

        return new
        {
            ok = true,
            location_id = resolvedLocationId,
            count = items.Count,
            items
        };
    }

    public async Task<object> AddToStagedCartAsync(KrogerStore store, string upc, int quantity)
    {
        var locationId = await store.GetDefaultStoreIdAsync();
        var snapshot = await _productsClient.GetProductByUpcAsync(store, upc, locationId);

        await store.AddStagedCartItemAsync(upc, quantity);
        _logger.LogInformation("Added item to staged cart: upc {Upc}, quantity {Quantity}, location {LocationId}", upc, quantity, locationId ?? "(default)");

        return new
        {
            ok = true,
            upc,
            quantity,
            location_id = locationId,
            staged = true,
            product = new
            {
                product_id = snapshot?.ProductId,
                description = snapshot?.Description,
                stock_level = snapshot?.StockLevel
            }
        };
    }

    public async Task<object> AddToCartAsync(
        KrogerStore store,
        string upc,
        int quantity,
        bool dryRun,
        bool allowUnknownStock)
    {
        var locationId = await store.GetDefaultStoreIdAsync();
        var snapshot = await _productsClient.GetProductByUpcAsync(store, upc, locationId);
        var inStock = IsInStock(snapshot?.StockLevel);

        if (snapshot is null)
        {
            _logger.LogWarning("Blocked add-to-cart because product was not found: upc {Upc}, quantity {Quantity}, location {LocationId}", upc, quantity, locationId ?? "(default)");
            return new
            {
                ok = false,
                blocked = true,
                reason = "product_not_found_for_store",
                upc,
                quantity,
                location_id = locationId
            };
        }

        if (inStock == false)
        {
            _logger.LogWarning("Blocked add-to-cart because item is out of stock: upc {Upc}, quantity {Quantity}, location {LocationId}", upc, quantity, locationId ?? "(default)");
            return new
            {
                ok = false,
                blocked = true,
                reason = "out_of_stock",
                upc,
                quantity,
                location_id = locationId,
                product_id = snapshot.ProductId,
                description = snapshot.Description,
                stock_level = snapshot.StockLevel,
                fulfillment = new
                {
                    curbside = snapshot.Curbside,
                    delivery = snapshot.Delivery,
                    in_store = snapshot.InStore,
                    ship_to_home = snapshot.ShipToHome
                }
            };
        }

        if (inStock is null && !allowUnknownStock)
        {
            _logger.LogWarning("Blocked add-to-cart because stock is unknown and override is disabled: upc {Upc}, quantity {Quantity}, location {LocationId}", upc, quantity, locationId ?? "(default)");
            return new
            {
                ok = false,
                blocked = true,
                reason = "unknown_stock",
                upc,
                quantity,
                location_id = locationId,
                product_id = snapshot.ProductId,
                description = snapshot.Description,
                stock_level = snapshot.StockLevel,
                fulfillment = new
                {
                    curbside = snapshot.Curbside,
                    delivery = snapshot.Delivery,
                    in_store = snapshot.InStore,
                    ship_to_home = snapshot.ShipToHome
                }
            };
        }

        var payload = new
        {
            items = new[]
            {
                new
                {
                    upc,
                    quantity
                }
            }
        };

        if (dryRun)
        {
            _logger.LogInformation("Prepared add-to-cart dry run: upc {Upc}, quantity {Quantity}, location {LocationId}", upc, quantity, locationId ?? "(default)");
            return new
            {
                ok = true,
                dry_run = true,
                blocked = false,
                method = "PUT",
                url = "https://api.kroger.com/v1/cart/add",
                upc,
                quantity,
                location_id = locationId,
                allow_unknown_stock = allowUnknownStock,
                product = new
                {
                    product_id = snapshot.ProductId,
                    description = snapshot.Description,
                    stock_level = snapshot.StockLevel
                },
                payload
            };
        }

        var accessToken = await _oauthClient.GetUsableAccessTokenAsync(store);
        var payloadJson = JsonSerializer.Serialize(payload);

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://api.kroger.com/v1/cart/add");
        request.Version = HttpVersion.Version20;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Live add-to-cart failed for upc {Upc}, quantity {Quantity}, location {LocationId} with status {StatusCode} {ReasonPhrase}. Body: {Body}",
                upc,
                quantity,
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

        object? parsed = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<object>(body);
            }
            catch
            {
                parsed = body;
            }
        }

        await store.AddTrackedCartItemAsync(upc, quantity);
        _logger.LogInformation("Added item to live Kroger cart: upc {Upc}, quantity {Quantity}, location {LocationId}", upc, quantity, locationId ?? "(default)");

        return new
        {
            ok = true,
            upc,
            quantity,
            location_id = locationId,
            stock_level = snapshot.StockLevel,
            allow_unknown_stock = allowUnknownStock,
            response = parsed
        };
    }

    public async Task<object> ApplySavedCartAsync(
        KrogerStore store,
        string name,
        bool dryRun,
        bool allowUnknownStock)
    {
        var savedCart = await store.GetSavedCartAsync(name);
        if (savedCart is null)
        {
            _logger.LogWarning("Saved cart apply requested for missing cart {Name}", name);
            return new
            {
                ok = false,
                error = "saved_cart_not_found",
                name
            };
        }

        var items = JsonSerializer.Deserialize<List<SavedCartItemPayload>>(savedCart.ItemsJson, SavedCartJsonOptions) ?? [];
        _logger.LogInformation("Applying saved cart {Name} with {ItemCount} items. dryRun={DryRun}, allowUnknownStock={AllowUnknownStock}", name, items.Count, dryRun, allowUnknownStock);
        var results = new List<object>();
        var successCount = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Upc) || item.Quantity <= 0)
            {
                results.Add(new
                {
                    ok = false,
                    blocked = true,
                    reason = "invalid_saved_cart_item",
                    upc = item.Upc,
                    quantity = item.Quantity
                });
                continue;
            }

            var result = await AddToCartAsync(store, item.Upc.Trim(), item.Quantity, dryRun, allowUnknownStock);
            results.Add(result);

            if (result.GetType().GetProperty("ok")?.GetValue(result) as bool? == true)
            {
                successCount++;
            }
        }

        return new
        {
            ok = true,
            name = savedCart.Name,
            dry_run = dryRun,
            count = items.Count,
            successful = successCount,
            results
        };
    }

    public async Task<object> CommitStagedCartAsync(
        KrogerStore store,
        bool dryRun,
        bool allowUnknownStock,
        bool clearOnSuccess)
    {
        var stagedItems = await store.GetStagedCartItemsAsync();
        _logger.LogInformation(
            "Committing staged cart with {ItemCount} items. dryRun={DryRun}, allowUnknownStock={AllowUnknownStock}, clearOnSuccess={ClearOnSuccess}",
            stagedItems.Count,
            dryRun,
            allowUnknownStock,
            clearOnSuccess);
        var results = new List<object>();
        var successCount = 0;

        foreach (var item in stagedItems)
        {
            var result = await AddToCartAsync(store, item.Upc, item.Quantity, dryRun, allowUnknownStock);
            results.Add(result);

            if (result.GetType().GetProperty("ok")?.GetValue(result) as bool? == true)
            {
                successCount++;
            }
        }

        if (!dryRun && clearOnSuccess && successCount == stagedItems.Count)
        {
            await store.ClearStagedCartAsync();
            _logger.LogInformation("Cleared staged cart after successful commit of {ItemCount} items", stagedItems.Count);
        }

        return new
        {
            ok = true,
            dry_run = dryRun,
            count = stagedItems.Count,
            successful = successCount,
            cleared_staged_cart = !dryRun && clearOnSuccess && successCount == stagedItems.Count,
            results
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

    private static bool? IsInStock(string? stockLevel)
    {
        return stockLevel switch
        {
            null => null,
            "HIGH" or "MEDIUM" or "LOW" => true,
            "TEMPORARILY_OUT_OF_STOCK" => false,
            _ => null
        };
    }
}
