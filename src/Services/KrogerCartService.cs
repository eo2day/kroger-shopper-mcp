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
    private sealed record SendAttemptResult(string Upc, int Quantity, bool Ok);
    private const string KrogerWebBaseUrl = "https://www.kroger.com";
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
        return await GetStagedCartInfoAsync(store, locationId);
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
        bool allowUnknownStock,
        bool trackLocally = true)
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

        if (trackLocally)
        {
            await store.AddStagedCartItemAsync(upc, quantity);
        }

        _logger.LogInformation(
            "Added item to live Kroger cart: upc {Upc}, quantity {Quantity}, location {LocationId}, trackLocally={TrackLocally}",
            upc,
            quantity,
            locationId ?? "(default)",
            trackLocally);

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
        var successfulItems = new List<(string Upc, int Quantity)>();

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

            if (IsSuccessful(result))
            {
                successCount++;
                successfulItems.Add((item.Upc.Trim(), item.Quantity));
            }
        }

        string? batchId = null;
        if (!dryRun && successfulItems.Count > 0)
        {
            batchId = await store.RecordKrogerSendBatchAsync($"saved_cart:{savedCart.Name}", successfulItems);
        }

        return new
        {
            ok = true,
            name = savedCart.Name,
            dry_run = dryRun,
            count = items.Count,
            successful = successCount,
            batch_id = batchId,
            results
        };
    }

    public async Task<object> GetSavedCartsBrowserDataAsync(KrogerStore store)
    {
        var locationId = await store.GetDefaultStoreIdAsync();
        var carts = await store.GetSavedCartsAsync();
        var parsedCarts = carts
            .OrderByDescending(cart => cart.UpdatedAtUtc)
            .Select(cart => new
            {
                Cart = cart,
                Items = JsonSerializer.Deserialize<List<SavedCartItemPayload>>(cart.ItemsJson, SavedCartJsonOptions) ?? []
            })
            .ToList();
        var snapshotsByUpc = await _productsClient.GetProductsByUpcsAsync(
            store,
            parsedCarts.SelectMany(static cart => cart.Items)
                .Where(static item => !string.IsNullOrWhiteSpace(item.Upc) && item.Quantity > 0)
                .Select(static item => item.Upc!),
            locationId);
        var cartViews = new List<object>();

        foreach (var cartEntry in parsedCarts)
        {
            var cart = cartEntry.Cart;
            var savedItems = cartEntry.Items;
            var itemViews = new List<object>();
            decimal? cartTotal = 0m;

            foreach (var savedItem in savedItems)
            {
                if (string.IsNullOrWhiteSpace(savedItem.Upc) || savedItem.Quantity <= 0)
                {
                    continue;
                }

                var upc = savedItem.Upc.Trim();
                snapshotsByUpc.TryGetValue(upc, out var snapshot);
                var unitPrice = snapshot?.PromoPrice ?? snapshot?.RegularPrice;
                var totalPrice = unitPrice is decimal price ? price * savedItem.Quantity : (decimal?)null;
                if (totalPrice is not null)
                {
                    cartTotal += totalPrice.Value;
                }

                itemViews.Add(new
                {
                    upc,
                    quantity = savedItem.Quantity,
                    product_id = snapshot?.ProductId,
                    description = snapshot?.Description,
                    brand = snapshot?.Brand,
                    size = snapshot?.Size,
                    image_url = snapshot?.ImageUrl,
                    product_url = BuildProductUrl(snapshot),
                    unit_price = unitPrice,
                    regular_price = snapshot?.RegularPrice,
                    promo_price = snapshot?.PromoPrice,
                    total_price = totalPrice
                });
            }

            cartViews.Add(new
            {
                name = cart.Name,
                created_at_utc = cart.CreatedAtUtc.ToString("O"),
                updated_at_utc = cart.UpdatedAtUtc.ToString("O"),
                item_count = itemViews.Count,
                total_quantity = savedItems.Where(item => !string.IsNullOrWhiteSpace(item.Upc) && item.Quantity > 0).Sum(item => item.Quantity),
                total_price = itemViews.Count == 0 ? null : cartTotal,
                items = itemViews
            });
        }

        return new
        {
            ok = true,
            location_id = locationId,
            count = cartViews.Count,
            carts = cartViews
        };
    }

    public async Task<object> GetCurrentCartBrowserDataAsync(KrogerStore store, string? locationId)
    {
        var stagedItems = await store.GetStagedCartItemsAsync();
        var resolvedLocationId = string.IsNullOrWhiteSpace(locationId)
            ? await store.GetDefaultStoreIdAsync()
            : locationId;
        var snapshotsByUpc = await _productsClient.GetProductsByUpcsAsync(
            store,
            stagedItems.Select(static item => item.Upc),
            resolvedLocationId);

        var itemViews = new List<object>();
        decimal? totalPrice = 0m;

        foreach (var stagedItem in stagedItems)
        {
            snapshotsByUpc.TryGetValue(stagedItem.Upc, out var snapshot);
            var unitPrice = snapshot?.PromoPrice ?? snapshot?.RegularPrice;
            var lineTotal = unitPrice is decimal price ? price * stagedItem.Quantity : (decimal?)null;
            if (lineTotal is not null)
            {
                totalPrice += lineTotal.Value;
            }

            itemViews.Add(new
            {
                upc = stagedItem.Upc,
                quantity = stagedItem.Quantity,
                updated_at_utc = stagedItem.UpdatedAtUtc.ToString("O"),
                product_id = snapshot?.ProductId,
                description = snapshot?.Description,
                brand = snapshot?.Brand,
                size = snapshot?.Size,
                image_url = snapshot?.ImageUrl,
                product_url = BuildProductUrl(snapshot),
                unit_price = unitPrice,
                regular_price = snapshot?.RegularPrice,
                promo_price = snapshot?.PromoPrice,
                total_price = lineTotal,
                stock_level = snapshot?.StockLevel,
                in_stock = IsInStock(snapshot?.StockLevel)
            });
        }

        return new
        {
            ok = true,
            location_id = resolvedLocationId,
            count = itemViews.Count,
            total_quantity = stagedItems.Sum(item => item.Quantity),
            total_price = itemViews.Count == 0 ? null : totalPrice,
            items = itemViews
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
        var successfulItems = new List<(string Upc, int Quantity)>();

        foreach (var item in stagedItems)
        {
            var result = await AddToCartAsync(store, item.Upc, item.Quantity, dryRun, allowUnknownStock);
            results.Add(result);

            if (IsSuccessful(result))
            {
                successCount++;
                successfulItems.Add((item.Upc, item.Quantity));
            }
        }

        string? batchId = null;
        if (!dryRun && successfulItems.Count > 0)
        {
            batchId = await store.RecordKrogerSendBatchAsync("staged_cart", successfulItems);
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
            batch_id = batchId,
            cleared_staged_cart = !dryRun && clearOnSuccess && successCount == stagedItems.Count,
            results
        };
    }

    public async Task<object> SendWorkingCartAsync(
        KrogerStore store,
        bool dryRun,
        bool allowUnknownStock,
        bool clearOnSuccess)
    {
        return await CommitStagedCartAsync(store, dryRun, allowUnknownStock, clearOnSuccess);
    }

    private static bool IsSuccessful(object result)
    {
        return result.GetType().GetProperty("ok")?.GetValue(result) as bool? == true;
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

    private static string? BuildProductUrl(KrogerProductSnapshot? snapshot)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.ProductPageUri))
        {
            return null;
        }

        return $"{KrogerWebBaseUrl}{snapshot.ProductPageUri}";
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
