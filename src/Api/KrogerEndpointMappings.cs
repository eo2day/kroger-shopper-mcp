using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Models;
using KrogerShopperMcp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KrogerShopperMcp.Api;

internal static class KrogerEndpointMappings
{
    public static void MapKrogerEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz", async (KrogerStore store) =>
        {
            var status = await store.GetTokenSummaryAsync();
            return Results.Json(new
            {
                status = "ok",
                service = "kroger-assistant",
                token_present = status is not null,
                expires_at_utc = status?.ExpiresAtUtc.ToString("O")
            });
        });

        app.MapGet("/", async (KrogerConfig config, KrogerStore store) =>
        {
            var status = await store.GetTokenSummaryAsync();
            return Results.Content(
                HtmlPages.RenderHomePage(config, status),
                "text/html; charset=utf-8");
        });

        app.MapGet("/authorize", async (HttpContext http, KrogerOAuthClient oauthClient, KrogerStore store) =>
        {
            var state = http.Request.Query["state"].ToString();
            if (string.IsNullOrWhiteSpace(state))
            {
                state = $"kroger-shopper-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            }

            var scopes = oauthClient.ParseScopes(http.Request.Query["scopes"].ToString());
            await store.SavePendingStateAsync(state, scopes);
            return Results.Redirect(oauthClient.BuildAuthorizeUrl(state, scopes));
        });

        app.MapGet("/callback", async (HttpContext http, KrogerStore store, KrogerOAuthClient oauthClient) =>
        {
            var error = http.Request.Query["error"].ToString();
            var errorDescription = http.Request.Query["error_description"].ToString();
            var state = http.Request.Query["state"].ToString();
            var code = http.Request.Query["code"].ToString();

            if (!string.IsNullOrWhiteSpace(error))
            {
                return Results.Content(
                    HtmlPages.RenderCallbackPage(false, $"OAuth error: {error}", errorDescription, state),
                    "text/html; charset=utf-8");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Content(
                    HtmlPages.RenderCallbackPage(
                        false,
                        "No authorization code was returned.",
                        "The callback reached the service, but Kroger did not provide a code.",
                        state),
                    "text/html; charset=utf-8");
            }

            try
            {
                var scope = await store.GetScopeForStateAsync(state) ?? oauthClient.DefaultScopeString;
                await oauthClient.ExchangeAuthorizationCodeAsync(store, code, state, scope);
                var status = await store.GetTokenSummaryAsync();

                return Results.Content(
                    HtmlPages.RenderCallbackPage(
                        true,
                        "Authorization complete.",
                        $"Tokens were exchanged and stored locally. Scope: {status?.Scope ?? scope}",
                        state),
                    "text/html; charset=utf-8");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"OAuth callback failed: {ex.Message}");
                return Results.Content(
                    HtmlPages.RenderCallbackPage(
                        false,
                        "Token exchange failed.",
                        "The authorization callback reached the service, but the token exchange did not complete.",
                        state),
                    "text/html; charset=utf-8");
            }
        });

        app.MapGet("/api/status", async (KrogerStore store) =>
        {
            var status = await store.GetTokenSummaryAsync();
            if (status is null)
            {
                return Results.Json(new { token_present = false });
            }

            return Results.Json(new
            {
                token_present = true,
                status.Scope,
                token_type = status.TokenType,
                expires_at_utc = status.ExpiresAtUtc.ToString("O"),
                access_token_length = status.AccessTokenLength,
                refresh_token_length = status.RefreshTokenLength,
                created_at_utc = status.CreatedAtUtc.ToString("O"),
                updated_at_utc = status.UpdatedAtUtc.ToString("O")
            });
        });

        app.MapPost("/api/refresh", async (KrogerOAuthClient oauthClient, KrogerStore store) =>
        {
            var token = await oauthClient.RefreshTokenAsync(store);
            return Results.Json(new
            {
                ok = true,
                expires_in = token.ExpiresIn,
                token_type = token.TokenType,
                scope = token.Scope
            });
        });

        app.MapGet("/api/search-products", async (HttpContext http, KrogerStore store, KrogerProductsClient productsClient) =>
        {
            var query = http.Request.Query["q"].ToString();
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.BadRequest(new { ok = false, error = "missing q" });
            }

            var limitRaw = http.Request.Query["limit"].ToString();
            var locationId = http.Request.Query["locationId"].ToString();
            var limit = int.TryParse(limitRaw, out var parsedLimit)
                ? Math.Clamp(parsedLimit, 1, 50)
                : 10;

            if (string.IsNullOrWhiteSpace(locationId))
            {
                locationId = await store.GetDefaultStoreIdAsync() ?? string.Empty;
            }

            return Results.Json(await productsClient.SearchProductsAsync(store, query, limit, locationId));
        });

        app.MapGet("/api/search-locations", async (HttpContext http, KrogerLocationsClient locationsClient, KrogerStore store) =>
        {
            var zipCode = http.Request.Query["zipCode"].ToString();
            var chain = http.Request.Query["chain"].ToString();
            var limitRaw = http.Request.Query["limit"].ToString();
            var limit = int.TryParse(limitRaw, out var parsedLimit)
                ? Math.Clamp(parsedLimit, 1, 50)
                : 10;

            if (string.IsNullOrWhiteSpace(zipCode))
            {
                return Results.BadRequest(new { ok = false, error = "missing zipCode" });
            }

            return Results.Json(await locationsClient.SearchLocationsAsync(store, zipCode, chain, limit));
        });

        app.MapPost("/api/set-default-store", async (KrogerSetStoreRequest request, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.LocationId))
            {
                return Results.BadRequest(new { ok = false, error = "missing locationId" });
            }

            await store.SetDefaultStoreAsync(request.LocationId.Trim(), request.Label);
            return Results.Json(new { ok = true, location_id = request.LocationId.Trim(), label = request.Label });
        });

        app.MapGet("/api/cart-info", async (HttpContext http, KrogerStore store, KrogerCartService cartService) =>
        {
            var locationId = http.Request.Query["locationId"].ToString();
            if (string.IsNullOrWhiteSpace(locationId))
            {
                locationId = await store.GetDefaultStoreIdAsync() ?? string.Empty;
            }

            return Results.Json(await cartService.GetCartInfoAsync(store, locationId));
        });

        app.MapPost("/api/add-to-cart", async (KrogerAddToCartRequest request, KrogerCartService cartService, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            var quantity = request.Quantity <= 0 ? 1 : request.Quantity;
            return Results.Json(await cartService.AddToCartAsync(
                store,
                request.Upc.Trim(),
                quantity,
                request.IsDryRun,
                request.IsAllowUnknownStock));
        });

        app.MapPost("/api/remove-tracked-cart-item", async (KrogerRemoveTrackedCartItemRequest request, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            var result = await store.RemoveTrackedCartItemAsync(request.Upc.Trim(), request.Quantity);
            return Results.Json(new
            {
                ok = true,
                scope = "tracked_cart_only",
                upc = request.Upc.Trim(),
                removed = result.Removed,
                remaining_quantity = result.RemainingQuantity
            });
        });

        app.MapPost("/api/command", async (KrogerCommandRequest request, KrogerStore store, KrogerOAuthClient oauthClient, KrogerProductsClient productsClient, KrogerLocationsClient locationsClient, KrogerCartService cartService) =>
        {
            var command = request.Command?.Trim().ToLowerInvariant();
            switch (command)
            {
                case "status":
                    {
                        var status = await store.GetTokenSummaryAsync();
                        return Results.Json(new
                        {
                            ok = true,
                            token_present = status is not null,
                            scope = status?.Scope,
                            expires_at_utc = status?.ExpiresAtUtc.ToString("O")
                        });
                    }
                case "auth-url":
                    {
                        var state = string.IsNullOrWhiteSpace(request.State)
                            ? $"kroger-shopper-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
                            : request.State.Trim();
                        var scopes = request.Scopes?.Length > 0 ? request.Scopes : oauthClient.DefaultScopes;
                        await store.SavePendingStateAsync(state, scopes);
                        return Results.Json(new { ok = true, state, url = oauthClient.BuildAuthorizeUrl(state, scopes) });
                    }
                case "refresh":
                    {
                        var token = await oauthClient.RefreshTokenAsync(store);
                        return Results.Json(new
                        {
                            ok = true,
                            expires_in = token.ExpiresIn,
                            token_type = token.TokenType,
                            scope = token.Scope
                        });
                    }
                case "search-products":
                    {
                        if (string.IsNullOrWhiteSpace(request.Query))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing query" });
                        }

                        return Results.Json(await productsClient.SearchProductsAsync(
                            store,
                            request.Query,
                            request.Limit is > 0 ? Math.Clamp(request.Limit.Value, 1, 50) : 10,
                            request.LocationId));
                    }
                case "search-locations":
                    {
                        if (string.IsNullOrWhiteSpace(request.ZipCode))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing zipCode" });
                        }

                        return Results.Json(await locationsClient.SearchLocationsAsync(
                            store,
                            request.ZipCode,
                            request.Chain,
                            request.Limit is > 0 ? Math.Clamp(request.Limit.Value, 1, 50) : 10));
                    }
                case "set-default-store":
                    {
                        if (string.IsNullOrWhiteSpace(request.LocationId))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing locationId" });
                        }

                        await store.SetDefaultStoreAsync(request.LocationId.Trim(), request.Label);
                        return Results.Json(new { ok = true, location_id = request.LocationId.Trim(), label = request.Label });
                    }
                case "cart-info":
                    {
                        var locationId = request.LocationId;
                        if (string.IsNullOrWhiteSpace(locationId))
                        {
                            locationId = await store.GetDefaultStoreIdAsync() ?? string.Empty;
                        }

                        return Results.Json(await cartService.GetCartInfoAsync(store, locationId));
                    }
                case "add-to-cart":
                    {
                        if (string.IsNullOrWhiteSpace(request.Upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing upc" });
                        }

                        var quantity = request.Quantity is > 0 ? request.Quantity.Value : 1;
                        return Results.Json(await cartService.AddToCartAsync(
                            store,
                            request.Upc.Trim(),
                            quantity,
                            request.IsDryRun,
                            request.IsAllowUnknownStock));
                    }
                case "remove-tracked-cart-item":
                    {
                        if (string.IsNullOrWhiteSpace(request.Upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing upc" });
                        }

                        var result = await store.RemoveTrackedCartItemAsync(request.Upc.Trim(), request.Quantity);
                        return Results.Json(new
                        {
                            ok = true,
                            scope = "tracked_cart_only",
                            upc = request.Upc.Trim(),
                            removed = result.Removed,
                            remaining_quantity = result.RemainingQuantity
                        });
                    }
                default:
                    return Results.BadRequest(new { ok = false, error = "unknown command" });
            }
        });
    }
}
