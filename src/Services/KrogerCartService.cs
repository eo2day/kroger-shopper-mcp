using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KrogerShopperMcp.Infrastructure;

namespace KrogerShopperMcp.Services;

internal sealed class KrogerCartService
{
    private readonly KrogerProductsClient _productsClient;
    private readonly KrogerOAuthClient _oauthClient;

    public KrogerCartService(KrogerProductsClient productsClient, KrogerOAuthClient oauthClient)
    {
        _productsClient = productsClient;
        _oauthClient = oauthClient;
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
