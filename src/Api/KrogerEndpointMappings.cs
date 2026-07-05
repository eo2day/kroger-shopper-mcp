using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Models;
using KrogerShopperMcp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KrogerShopperMcp.Api;

internal static class KrogerEndpointMappings
{
    private const string FaviconSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
          <rect width="64" height="64" fill="#ffffff"/>
          <path d="M16 10h10v18.5L42.5 10H55L37.5 30.5 56 54H43.5L29.5 36 26 39.5V54H16z" fill="#3950d4"/>
        </svg>
        """;

    public static void MapKrogerEndpoints(this WebApplication app)
    {
        app.MapGet("/favicon.svg", () => Results.Content(FaviconSvg, "image/svg+xml; charset=utf-8"));
        app.MapGet("/favicon.ico", () => Results.Redirect("/favicon.svg", permanent: false));

        app.MapGet("/setup", async (KrogerStore store) =>
        {
            if (await store.GetWebCredentialAsync() is not null)
            {
                return Results.Redirect("/");
            }

            return HtmlContentNoCache(HtmlPages.RenderSetupPage());
        });

        app.MapPost("/setup", async (HttpContext http, KrogerStore store, KrogerWebAuthService webAuth, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("KrogerWebUi");
            if (await store.GetWebCredentialAsync() is not null)
            {
                return Results.Redirect("/");
            }

            var form = await http.Request.ReadFormAsync();
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var result = await webAuth.CreateInitialCredentialAsync(store, username, password);
            if (!result.Ok)
            {
                logger.LogWarning("Initial web credential setup failed for username {Username}: {Error}", username.Trim(), result.Error);
                return HtmlContentNoCache(HtmlPages.RenderSetupPage(result.Error));
            }

            var sessionId = await webAuth.CreateSessionAsync(store, username.Trim());
            logger.LogInformation("Initial web credential created for username {Username}", username.Trim());
            SetSessionCookie(http, sessionId);
            return Results.Redirect("/");
        });

        app.MapGet("/login", async (KrogerStore store) =>
        {
            var credential = await store.GetWebCredentialAsync();
            if (credential is null)
            {
                return Results.Redirect("/setup");
            }

            return HtmlContentNoCache(HtmlPages.RenderLoginPage(credential.Username));
        });

        app.MapPost("/login", async (HttpContext http, KrogerStore store, KrogerWebAuthService webAuth, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("KrogerWebUi");
            var credential = await store.GetWebCredentialAsync();
            if (credential is null)
            {
                return Results.Redirect("/setup");
            }

            var form = await http.Request.ReadFormAsync();
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var isValid = await webAuth.ValidateCredentialAsync(store, username, password);
            if (!isValid)
            {
                logger.LogWarning("Web login failed for username {Username}", username.Trim());
                return HtmlContentNoCache(HtmlPages.RenderLoginPage(credential.Username, "Invalid password."));
            }

            var sessionId = await webAuth.CreateSessionAsync(store, credential.Username);
            logger.LogInformation("Web login succeeded for username {Username}", credential.Username);
            SetSessionCookie(http, sessionId);
            return Results.Redirect("/");
        });

        app.MapGet("/change-password", async (HttpContext http, KrogerStore store) =>
        {
            var authResult = await RequireWebUiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            return HtmlContentNoCache(HtmlPages.RenderChangePasswordPage(authResult.Username!));
        });

        app.MapPost("/change-password", async (HttpContext http, KrogerStore store, KrogerWebAuthService webAuth, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("KrogerWebUi");
            var authResult = await RequireWebUiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            var form = await http.Request.ReadFormAsync();
            var currentPassword = form["current_password"].ToString();
            var newPassword = form["new_password"].ToString();
            var result = await webAuth.ChangePasswordAsync(store, currentPassword, newPassword);
            if (!result.Ok)
            {
                logger.LogWarning("Password change failed for username {Username}: {Error}", authResult.Username!, result.Error);
                return HtmlContentNoCache(HtmlPages.RenderChangePasswordPage(authResult.Username!, result.Error));
            }

            logger.LogInformation("Password changed for username {Username}", authResult.Username!);
            return HtmlContentNoCache(HtmlPages.RenderChangePasswordPage(authResult.Username!, null, "Password updated."));
        });

        app.MapGet("/logout", async (HttpContext http, KrogerStore store, KrogerWebAuthService webAuth, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("KrogerWebUi");
            var sessionId = http.Request.Cookies[KrogerWebAuthService.SessionCookieName];
            await webAuth.DeleteSessionAsync(store, sessionId);
            ClearSessionCookie(http);
            logger.LogInformation("Web logout completed");
            return Results.Redirect("/login");
        });

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

        app.MapGet("/", async (HttpContext http, KrogerConfig config, KrogerStore store) =>
        {
            var authResult = await RequireWebUiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            var status = await store.GetTokenSummaryAsync();
            return HtmlContentNoCache(
                HtmlPages.RenderHomePage(config, status, authResult.Username!),
                "text/html; charset=utf-8");
        });

        app.MapGet("/saved-carts", async (HttpContext http, KrogerStore store) =>
        {
            var authResult = await RequireWebUiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            return HtmlContentNoCache(
                HtmlPages.RenderSavedCartsPage(authResult.Username!),
                "text/html; charset=utf-8");
        });

        app.MapGet("/current-cart", async (HttpContext http, KrogerStore store) =>
        {
            var authResult = await RequireWebUiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            return HtmlContentNoCache(
                HtmlPages.RenderCurrentCartPage(authResult.Username!),
                "text/html; charset=utf-8");
        });

        app.MapGet("/sent-history", async (HttpContext http, KrogerStore store) =>
        {
            var authResult = await RequireWebUiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            return HtmlContentNoCache(
                HtmlPages.RenderSentHistoryPage(authResult.Username!),
                "text/html; charset=utf-8");
        });

        app.MapGet("/authorize", async (HttpContext http, KrogerOAuthClient oauthClient, KrogerStore store, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("KrogerOAuthFlow");
            var authResult = await RequireWebUiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            var state = http.Request.Query["state"].ToString();
            if (string.IsNullOrWhiteSpace(state))
            {
                state = $"kroger-shopper-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            }

            var scopes = oauthClient.ParseScopes(http.Request.Query["scopes"].ToString());
            await store.SavePendingStateAsync(state, scopes);
            logger.LogInformation("Starting Kroger authorize redirect for state {State} with scopes {Scopes}", state, string.Join(' ', scopes));
            return Results.Redirect(oauthClient.BuildAuthorizeUrl(state, scopes));
        });

        app.MapGet("/callback", async (HttpContext http, KrogerStore store, KrogerOAuthClient oauthClient, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("KrogerOAuthFlow");
            var authResult = await RequireWebUiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            var error = http.Request.Query["error"].ToString();
            var errorDescription = http.Request.Query["error_description"].ToString();
            var state = http.Request.Query["state"].ToString();
            var code = http.Request.Query["code"].ToString();

            if (!string.IsNullOrWhiteSpace(error))
            {
                logger.LogWarning("Kroger callback returned oauth error {Error} for state {State}: {ErrorDescription}", error, state ?? "(none)", errorDescription);
                return Results.Content(
                    HtmlPages.RenderCallbackPage(false, $"OAuth error: {error}", errorDescription, state),
                    "text/html; charset=utf-8");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                logger.LogWarning("Kroger callback reached service without authorization code for state {State}", state ?? "(none)");
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
                logger.LogInformation("Kroger callback exchange completed for state {State}", state ?? "(none)");

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
                logger.LogError(ex, "Kroger callback token exchange failed for state {State}", state ?? "(none)");
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

        app.MapGet("/api/staged-cart", async (HttpContext http, KrogerStore store, KrogerCartService cartService) =>
        {
            var locationId = http.Request.Query["locationId"].ToString();
            if (string.IsNullOrWhiteSpace(locationId))
            {
                locationId = await store.GetDefaultStoreIdAsync() ?? string.Empty;
            }

            return Results.Json(await cartService.GetStagedCartInfoAsync(store, locationId));
        });

        app.MapPost("/api/add-to-staged-cart", async (KrogerAddToStagedCartRequest request, KrogerCartService cartService, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            var quantity = request.Quantity <= 0 ? 1 : request.Quantity;
            return Results.Json(await cartService.AddToStagedCartAsync(store, request.Upc.Trim(), quantity));
        });

        app.MapPost("/api/remove-staged-cart-item", async (KrogerRemoveStagedCartItemRequest request, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            var result = await store.RemoveStagedCartItemAsync(request.Upc.Trim(), request.Quantity);
            return Results.Json(new
            {
                ok = true,
                scope = "staged_cart_only",
                upc = request.Upc.Trim(),
                removed = result.Removed,
                remaining_quantity = result.RemainingQuantity
            });
        });

        app.MapPost("/api/clear-staged-cart", async (KrogerStore store) =>
        {
            var removedCount = await store.ClearStagedCartAsync();
            return Results.Json(new { ok = true, action = "clear_staged_cart", removed = removedCount });
        });

        app.MapPost("/api/remove-tracked-cart-item", async (KrogerRemoveTrackedCartItemRequest request, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            var result = await store.RemoveStagedCartItemAsync(request.Upc.Trim(), request.Quantity);
            return Results.Json(new
            {
                ok = true,
                scope = "working_cart",
                upc = request.Upc.Trim(),
                removed = result.Removed,
                remaining_quantity = result.RemainingQuantity
            });
        });

        app.MapGet("/api/sent-to-kroger-history", async (HttpContext http, KrogerStore store) =>
        {
            var limitRaw = http.Request.Query["limit"].ToString();
            var limit = int.TryParse(limitRaw, out var parsedLimit)
                ? Math.Clamp(parsedLimit, 1, 500)
                : 100;
            var items = await store.GetKrogerSendHistoryAsync(limit);
            return Results.Json(new
            {
                ok = true,
                count = items.Count,
                items = items.Select(item => new
                {
                    id = item.Id,
                    batch_id = item.BatchId,
                    source = item.Source,
                    upc = item.Upc,
                    quantity = item.Quantity,
                    sent_at_utc = item.SentAtUtc.ToString("O")
                })
            });
        });

        app.MapPost("/api/save-cart", async (KrogerSaveCartRequest request, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            var savedCart = await store.SaveStagedCartAsync(request.Name.Trim());
            var items = JsonSerializer.Deserialize<object>(savedCart.ItemsJson);
            return Results.Json(new
            {
                ok = true,
                name = savedCart.Name,
                created_at_utc = savedCart.CreatedAtUtc.ToString("O"),
                updated_at_utc = savedCart.UpdatedAtUtc.ToString("O"),
                items
            });
        });

        app.MapPost("/api/save-staged-cart", async (KrogerSaveStagedCartRequest request, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            var savedCart = await store.SaveStagedCartAsync(request.Name.Trim());
            var items = JsonSerializer.Deserialize<object>(savedCart.ItemsJson);
            return Results.Json(new
            {
                ok = true,
                name = savedCart.Name,
                created_at_utc = savedCart.CreatedAtUtc.ToString("O"),
                updated_at_utc = savedCart.UpdatedAtUtc.ToString("O"),
                items
            });
        });

        app.MapGet("/api/saved-carts", async (HttpContext http, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            var carts = await store.GetSavedCartsAsync();
            return Results.Json(new
            {
                ok = true,
                count = carts.Count,
                carts = carts.Select(cart => new
                {
                    name = cart.Name,
                    created_at_utc = cart.CreatedAtUtc.ToString("O"),
                    updated_at_utc = cart.UpdatedAtUtc.ToString("O"),
                    items = JsonSerializer.Deserialize<object>(cart.ItemsJson)
                })
            });
        });

        app.MapGet("/api/saved-carts-view", async (HttpContext http, KrogerStore store, KrogerCartService cartService) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            return Results.Json(await cartService.GetSavedCartsBrowserDataAsync(store));
        });

        app.MapPost("/api/saved-carts-add-item", async (HttpContext http, KrogerAddSavedCartItemRequest request, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            if (!TryResolveUpc(request.Identifier, out var upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing_or_invalid_identifier" });
            }

            var quantity = request.Quantity <= 0 ? 1 : request.Quantity;
            var savedCart = await store.AddSavedCartItemAsync(request.Name.Trim(), upc, quantity);
            if (savedCart is null)
            {
                return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Name.Trim() });
            }

            return Results.Json(new
            {
                ok = true,
                name = savedCart.Name,
                upc,
                quantity,
                updated_at_utc = savedCart.UpdatedAtUtc.ToString("O")
            });
        });

        app.MapPost("/api/saved-carts-rename", async (HttpContext http, KrogerRenameSavedCartRequest request, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            if (string.IsNullOrWhiteSpace(request.NewName))
            {
                return Results.BadRequest(new { ok = false, error = "missing newName" });
            }

            var cart = await store.RenameSavedCartAsync(request.Name.Trim(), request.NewName.Trim());
            if (cart is null)
            {
                return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Name.Trim() });
            }

            return Results.Json(new
            {
                ok = true,
                old_name = request.Name.Trim(),
                name = cart.Name,
                created_at_utc = cart.CreatedAtUtc.ToString("O"),
                updated_at_utc = cart.UpdatedAtUtc.ToString("O")
            });
        });

        app.MapPost("/api/saved-carts-duplicate", async (HttpContext http, KrogerDuplicateSavedCartRequest request, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            if (string.IsNullOrWhiteSpace(request.NewName))
            {
                return Results.BadRequest(new { ok = false, error = "missing newName" });
            }

            var cart = await store.DuplicateSavedCartAsync(request.Name.Trim(), request.NewName.Trim());
            if (cart is null)
            {
                return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Name.Trim() });
            }

            return Results.Json(new
            {
                ok = true,
                source_name = request.Name.Trim(),
                name = cart.Name,
                created_at_utc = cart.CreatedAtUtc.ToString("O"),
                updated_at_utc = cart.UpdatedAtUtc.ToString("O")
            });
        });

        app.MapPost("/api/saved-carts-delete", async (HttpContext http, KrogerDeleteSavedCartRequest request, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            var deleted = await store.DeleteSavedCartAsync(request.Name.Trim());
            if (!deleted)
            {
                return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Name.Trim() });
            }

            return Results.Json(new
            {
                ok = true,
                name = request.Name.Trim(),
                deleted = true
            });
        });

        app.MapPost("/api/saved-carts-set-quantity", async (HttpContext http, KrogerSetSavedCartItemQuantityRequest request, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            if (request.Quantity < 0)
            {
                return Results.BadRequest(new { ok = false, error = "quantity_must_be_zero_or_greater" });
            }

            var name = request.Name.Trim();
            var upc = request.Upc.Trim();
            var savedCart = await store.SetSavedCartItemQuantityAsync(name, upc, request.Quantity);
            if (savedCart is null)
            {
                return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name });
            }

            return Results.Json(new
            {
                ok = true,
                name,
                upc,
                quantity = request.Quantity,
                removed = request.Quantity == 0,
                updated_at_utc = savedCart.UpdatedAtUtc.ToString("O")
            });
        });

        app.MapPost("/api/saved-carts-remove-item", async (HttpContext http, KrogerRemoveSavedCartItemRequest request, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            var name = request.Name.Trim();
            var upc = request.Upc.Trim();
            var result = await store.RemoveSavedCartItemAsync(name, upc);
            if (result.Cart is null)
            {
                return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name });
            }

            return Results.Json(new
            {
                ok = true,
                name,
                upc,
                removed = result.Removed,
                updated_at_utc = result.Cart.UpdatedAtUtc.ToString("O")
            });
        });

        app.MapGet("/api/current-cart-view", async (HttpContext http, KrogerStore store, KrogerCartService cartService) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            var locationId = http.Request.Query["locationId"].ToString();
            if (string.IsNullOrWhiteSpace(locationId))
            {
                locationId = await store.GetDefaultStoreIdAsync() ?? string.Empty;
            }

            return Results.Json(await cartService.GetCurrentCartBrowserDataAsync(store, locationId));
        });

        app.MapPost("/api/current-cart-add-item", async (HttpContext http, KrogerAddTrackedCartItemRequest request, KrogerCartService cartService, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (!TryResolveUpc(request.Identifier, out var upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing_or_invalid_identifier" });
            }

            var quantity = request.Quantity <= 0 ? 1 : request.Quantity;
            return Results.Json(await cartService.AddToStagedCartAsync(store, upc, quantity));
        });

        app.MapPost("/api/current-cart-set-quantity", async (HttpContext http, KrogerSetTrackedCartItemQuantityRequest request, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            if (request.Quantity < 0)
            {
                return Results.BadRequest(new { ok = false, error = "quantity_must_be_zero_or_greater" });
            }

            var upc = request.Upc.Trim();
            var quantity = await store.SetStagedCartItemQuantityAsync(upc, request.Quantity);
            return Results.Json(new
            {
                ok = true,
                upc,
                quantity,
                removed = quantity == 0
            });
        });

        app.MapPost("/api/current-cart-remove-item", async (HttpContext http, KrogerRemoveTrackedCartItemRequest request, KrogerStore store) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            if (string.IsNullOrWhiteSpace(request.Upc))
            {
                return Results.BadRequest(new { ok = false, error = "missing upc" });
            }

            var upc = request.Upc.Trim();
            var result = await store.RemoveStagedCartItemAsync(upc, null);
            return Results.Json(new
            {
                ok = true,
                upc,
                removed = result.Removed,
                remaining_quantity = result.RemainingQuantity
            });
        });

        app.MapPost("/api/send-tracked-cart", async (HttpContext http, KrogerStore store, KrogerCartService cartService) =>
        {
            var authResult = await RequireWebUiApiAuthAsync(http, store);
            if (authResult.Result is not null)
            {
                return authResult.Result;
            }

            return Results.Json(await cartService.SendWorkingCartAsync(
                store,
                dryRun: false,
                allowUnknownStock: true,
                clearOnSuccess: true));
        });

        app.MapPost("/api/apply-saved-cart", async (KrogerApplySavedCartRequest request, KrogerStore store, KrogerCartService cartService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            return Results.Json(await cartService.ApplySavedCartAsync(
                store,
                request.Name.Trim(),
                request.IsDryRun,
                request.IsAllowUnknownStock));
        });

        app.MapPost("/api/load-saved-cart-to-staged", async (KrogerLoadSavedCartToStagedRequest request, KrogerStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { ok = false, error = "missing name" });
            }

            var loadedCount = await store.LoadSavedCartIntoStagedAsync(request.Name.Trim(), request.IsReplaceExisting);
            if (loadedCount < 0)
            {
                return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Name.Trim() });
            }

            return Results.Json(new
            {
                ok = true,
                name = request.Name.Trim(),
                replace_existing = request.IsReplaceExisting,
                loaded = loadedCount
            });
        });

        app.MapPost("/api/commit-staged-cart", async (KrogerCommitStagedCartRequest request, KrogerStore store, KrogerCartService cartService) =>
        {
            return Results.Json(await cartService.CommitStagedCartAsync(
                store,
                request.IsDryRun,
                request.IsAllowUnknownStock,
                request.IsClearOnSuccess));
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
                        var identifier = !string.IsNullOrWhiteSpace(request.Upc) ? request.Upc : request.Identifier;
                        if (!TryResolveUpc(identifier, out var upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing_or_invalid_identifier" });
                        }

                        var quantity = request.Quantity is > 0 ? request.Quantity.Value : 1;
                        return Results.Json(await cartService.AddToCartAsync(
                            store,
                            upc,
                            quantity,
                            request.IsDryRun,
                            request.IsAllowUnknownStock));
                    }
                case "staged-cart":
                    {
                        var locationId = request.LocationId;
                        if (string.IsNullOrWhiteSpace(locationId))
                        {
                            locationId = await store.GetDefaultStoreIdAsync() ?? string.Empty;
                        }

                        return Results.Json(await cartService.GetStagedCartInfoAsync(store, locationId));
                    }
                case "add-to-staged-cart":
                    {
                        if (string.IsNullOrWhiteSpace(request.Upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing upc" });
                        }

                        var quantity = request.Quantity is > 0 ? request.Quantity.Value : 1;
                        return Results.Json(await cartService.AddToStagedCartAsync(
                            store,
                            request.Upc.Trim(),
                            quantity));
                    }
                case "remove-staged-cart-item":
                    {
                        if (string.IsNullOrWhiteSpace(request.Upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing upc" });
                        }

                        var result = await store.RemoveStagedCartItemAsync(request.Upc.Trim(), request.Quantity);
                        return Results.Json(new
                        {
                            ok = true,
                            scope = "staged_cart_only",
                            upc = request.Upc.Trim(),
                            removed = result.Removed,
                            remaining_quantity = result.RemainingQuantity
                        });
                    }
                case "clear-staged-cart":
                    {
                        var removedCount = await store.ClearStagedCartAsync();
                        return Results.Json(new { ok = true, action = "clear_staged_cart", removed = removedCount });
                    }
                case "remove-tracked-cart-item":
                    {
                        if (string.IsNullOrWhiteSpace(request.Upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing upc" });
                        }

                        var result = await store.RemoveStagedCartItemAsync(request.Upc.Trim(), request.Quantity);
                        return Results.Json(new
                        {
                            ok = true,
                            scope = "working_cart",
                            upc = request.Upc.Trim(),
                            removed = result.Removed,
                            remaining_quantity = result.RemainingQuantity
                        });
                    }
                case "mark-purchased":
                    {
                        return Results.BadRequest(new { ok = false, error = "purchased_items_removed_use_sent_to_kroger_history" });
                    }
                case "clear-tracked-cart":
                    {
                        var removedCount = await store.ClearStagedCartAsync();
                        return Results.Json(new { ok = true, action = "clear_working_cart", removed = removedCount });
                    }
                case "send-current-cart":
                    {
                        return Results.Json(await cartService.SendWorkingCartAsync(
                            store,
                            request.IsDryRun,
                            true,
                            true));
                    }
                case "save-cart":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        var savedCart = await store.SaveStagedCartAsync(request.Label.Trim());
                        return Results.Json(new
                        {
                            ok = true,
                            name = savedCart.Name,
                            created_at_utc = savedCart.CreatedAtUtc.ToString("O"),
                            updated_at_utc = savedCart.UpdatedAtUtc.ToString("O"),
                            items = JsonSerializer.Deserialize<object>(savedCart.ItemsJson)
                        });
                    }
                case "save-staged-cart":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        var savedCart = await store.SaveStagedCartAsync(request.Label.Trim());
                        return Results.Json(new
                        {
                            ok = true,
                            name = savedCart.Name,
                            created_at_utc = savedCart.CreatedAtUtc.ToString("O"),
                            updated_at_utc = savedCart.UpdatedAtUtc.ToString("O"),
                            items = JsonSerializer.Deserialize<object>(savedCart.ItemsJson)
                        });
                    }
                case "saved-carts":
                    {
                        var carts = await store.GetSavedCartsAsync();
                        return Results.Json(new
                        {
                            ok = true,
                            count = carts.Count,
                            carts = carts.Select(cart => new
                            {
                                name = cart.Name,
                                created_at_utc = cart.CreatedAtUtc.ToString("O"),
                                updated_at_utc = cart.UpdatedAtUtc.ToString("O"),
                                items = JsonSerializer.Deserialize<object>(cart.ItemsJson)
                            })
                        });
                    }
                case "saved-cart-set-quantity":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        if (string.IsNullOrWhiteSpace(request.Upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing upc" });
                        }

                        if (request.Quantity is null || request.Quantity < 0)
                        {
                            return Results.BadRequest(new { ok = false, error = "quantity_must_be_zero_or_greater" });
                        }

                        var savedCart = await store.SetSavedCartItemQuantityAsync(request.Label.Trim(), request.Upc.Trim(), request.Quantity.Value);
                        if (savedCart is null)
                        {
                            return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Label.Trim() });
                        }

                        return Results.Json(new
                        {
                            ok = true,
                            name = savedCart.Name,
                            upc = request.Upc.Trim(),
                            quantity = request.Quantity.Value,
                            removed = request.Quantity.Value == 0,
                            updated_at_utc = savedCart.UpdatedAtUtc.ToString("O")
                        });
                    }
                case "saved-cart-add-item":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        var identifier = !string.IsNullOrWhiteSpace(request.Upc) ? request.Upc : request.Identifier;
                        if (!TryResolveUpc(identifier, out var upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing_or_invalid_identifier" });
                        }

                        var quantity = request.Quantity is > 0 ? request.Quantity.Value : 1;
                        var savedCart = await store.AddSavedCartItemAsync(request.Label.Trim(), upc, quantity);
                        if (savedCart is null)
                        {
                            return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Label.Trim() });
                        }

                        return Results.Json(new
                        {
                            ok = true,
                            name = savedCart.Name,
                            upc,
                            quantity,
                            updated_at_utc = savedCart.UpdatedAtUtc.ToString("O")
                        });
                    }
                case "rename-saved-cart":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        if (string.IsNullOrWhiteSpace(request.Name))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing name" });
                        }

                        var cart = await store.RenameSavedCartAsync(request.Label.Trim(), request.Name.Trim());
                        if (cart is null)
                        {
                            return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Label.Trim() });
                        }

                        return Results.Json(new
                        {
                            ok = true,
                            old_name = request.Label.Trim(),
                            name = cart.Name,
                            created_at_utc = cart.CreatedAtUtc.ToString("O"),
                            updated_at_utc = cart.UpdatedAtUtc.ToString("O")
                        });
                    }
                case "duplicate-saved-cart":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        if (string.IsNullOrWhiteSpace(request.Name))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing name" });
                        }

                        var cart = await store.DuplicateSavedCartAsync(request.Label.Trim(), request.Name.Trim());
                        if (cart is null)
                        {
                            return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Label.Trim() });
                        }

                        return Results.Json(new
                        {
                            ok = true,
                            source_name = request.Label.Trim(),
                            name = cart.Name,
                            created_at_utc = cart.CreatedAtUtc.ToString("O"),
                            updated_at_utc = cart.UpdatedAtUtc.ToString("O")
                        });
                    }
                case "delete-saved-cart":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        var deleted = await store.DeleteSavedCartAsync(request.Label.Trim());
                        if (!deleted)
                        {
                            return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Label.Trim() });
                        }

                        return Results.Json(new
                        {
                            ok = true,
                            name = request.Label.Trim(),
                            deleted = true
                        });
                    }
                case "saved-cart-remove-item":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        if (string.IsNullOrWhiteSpace(request.Upc))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing upc" });
                        }

                        var result = await store.RemoveSavedCartItemAsync(request.Label.Trim(), request.Upc.Trim());
                        if (result.Cart is null)
                        {
                            return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Label.Trim() });
                        }

                        return Results.Json(new
                        {
                            ok = true,
                            name = result.Cart.Name,
                            upc = request.Upc.Trim(),
                            removed = result.Removed,
                            updated_at_utc = result.Cart.UpdatedAtUtc.ToString("O")
                        });
                    }
                case "apply-saved-cart":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        return Results.Json(await cartService.ApplySavedCartAsync(
                            store,
                            request.Label.Trim(),
                            request.IsDryRun,
                            request.IsAllowUnknownStock));
                    }
                case "load-saved-cart-to-staged":
                    {
                        if (string.IsNullOrWhiteSpace(request.Label))
                        {
                            return Results.BadRequest(new { ok = false, error = "missing label" });
                        }

                        var loadedCount = await store.LoadSavedCartIntoStagedAsync(request.Label.Trim(), false);
                        if (loadedCount < 0)
                        {
                            return Results.NotFound(new { ok = false, error = "saved_cart_not_found", name = request.Label.Trim() });
                        }

                        return Results.Json(new
                        {
                            ok = true,
                            name = request.Label.Trim(),
                            replace_existing = false,
                            loaded = loadedCount
                        });
                    }
                case "commit-staged-cart":
                    {
                        return Results.Json(await cartService.CommitStagedCartAsync(
                            store,
                            request.IsDryRun,
                            request.IsAllowUnknownStock,
                            true));
                    }
                case "purchased-items":
                    {
                        var items = await store.GetKrogerSendHistoryAsync(request.Limit ?? 100);
                        return Results.Json(new
                        {
                            ok = true,
                            count = items.Count,
                            items = items.Select(item => new
                            {
                                id = item.Id,
                                batch_id = item.BatchId,
                                source = item.Source,
                                upc = item.Upc,
                                quantity = item.Quantity,
                                sent_at_utc = item.SentAtUtc.ToString("O")
                            })
                        });
                    }
                default:
                    return Results.BadRequest(new { ok = false, error = "unknown command" });
            }
        });
    }

    private static async Task<(IResult? Result, string? Username)> RequireWebUiAuthAsync(HttpContext? http, KrogerStore store)
    {
        if (http is null)
        {
            return (Results.StatusCode(StatusCodes.Status500InternalServerError), null);
        }

        var credential = await store.GetWebCredentialAsync();
        if (credential is null)
        {
            return (Results.Redirect("/setup"), null);
        }

        var webAuth = http.RequestServices.GetRequiredService<KrogerWebAuthService>();
        var sessionId = http.Request.Cookies[KrogerWebAuthService.SessionCookieName];
        var isAuthed = await webAuth.IsAuthenticatedAsync(store, sessionId);
        if (!isAuthed)
        {
            return (Results.Redirect("/login"), null);
        }

        return (null, credential.Username);
    }

    private static IResult HtmlContentNoCache(string html, string contentType = "text/html; charset=utf-8")
    {
        return new NoCacheResult(Results.Content(html, contentType));
    }

    private sealed class NoCacheResult(IResult inner) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            httpContext.Response.Headers["Pragma"] = "no-cache";
            httpContext.Response.Headers["Expires"] = "0";
            httpContext.Response.Headers["Vary"] = "Cookie";
            await inner.ExecuteAsync(httpContext);
        }
    }

    private static bool TryResolveUpc(string? identifier, out string upc)
    {
        upc = string.Empty;
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        var trimmed = identifier.Trim();
        if (trimmed.All(char.IsDigit) && trimmed.Length is >= 8 and <= 14)
        {
            upc = trimmed;
            return true;
        }

        var match = Regex.Match(trimmed, @"(?<!\d)(\d{8,14})(?!\d)");
        if (!match.Success)
        {
            return false;
        }

        upc = match.Groups[1].Value;
        return true;
    }

    private static async Task<(IResult? Result, string? Username)> RequireWebUiApiAuthAsync(HttpContext? http, KrogerStore store)
    {
        if (http is null)
        {
            return (Results.StatusCode(StatusCodes.Status500InternalServerError), null);
        }

        var credential = await store.GetWebCredentialAsync();
        if (credential is null)
        {
            return (Results.Json(new { ok = false, error = "setup_required" }, statusCode: StatusCodes.Status401Unauthorized), null);
        }

        var webAuth = http.RequestServices.GetRequiredService<KrogerWebAuthService>();
        var sessionId = http.Request.Cookies[KrogerWebAuthService.SessionCookieName];
        var isAuthed = await webAuth.IsAuthenticatedAsync(store, sessionId);
        if (!isAuthed)
        {
            return (Results.Json(new { ok = false, error = "login_required" }, statusCode: StatusCodes.Status401Unauthorized), null);
        }

        return (null, credential.Username);
    }

    private static void SetSessionCookie(HttpContext http, string sessionId)
    {
        http.Response.Cookies.Append(KrogerWebAuthService.SessionCookieName, sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = IsSecureRequest(http),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(14),
            IsEssential = true,
            Path = "/"
        });
    }

    private static void ClearSessionCookie(HttpContext http)
    {
        http.Response.Cookies.Delete(KrogerWebAuthService.SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = IsSecureRequest(http),
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }

    private static bool IsSecureRequest(HttpContext http)
    {
        if (http.Request.IsHttps)
        {
            return true;
        }

        var forwardedProto = http.Request.Headers["X-Forwarded-Proto"].ToString();
        return string.Equals(forwardedProto, "https", StringComparison.OrdinalIgnoreCase);
    }
}
