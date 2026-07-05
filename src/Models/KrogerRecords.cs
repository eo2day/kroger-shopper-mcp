using System.Text.Json.Serialization;

namespace KrogerShopperMcp.Models;

internal sealed record KrogerTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("scope")] string? Scope);

internal sealed record StoredToken(
    string AccessToken,
    string? RefreshToken,
    string Scope,
    string TokenType,
    DateTimeOffset ExpiresAtUtc);

internal sealed record StagedCartItem(
    string Upc,
    int Quantity,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record TrackedCartRemovalResult(
    int Removed,
    int RemainingQuantity);

internal sealed record KrogerSendHistoryItem(
    long Id,
    string BatchId,
    string Source,
    string Upc,
    int Quantity,
    DateTimeOffset SentAtUtc);

internal sealed record WebCredential(
    string Username,
    string PasswordHash,
    string PasswordSalt,
    int PasswordIterations,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record WebSession(
    string SessionId,
    string Username,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc);

internal sealed record SavedCart(
    string Name,
    string ItemsJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record TokenSummary(
    string Scope,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int AccessTokenLength,
    int RefreshTokenLength);

internal sealed record KrogerProductSnapshot(
    string? ProductId,
    string Upc,
    string? ProductPageUri,
    string? ImageUrl,
    string? Description,
    string? Brand,
    string? Size,
    string? SoldBy,
    decimal? RegularPrice,
    decimal? PromoPrice,
    string? StockLevel,
    bool? Curbside,
    bool? Delivery,
    bool? InStore,
    bool? ShipToHome);
