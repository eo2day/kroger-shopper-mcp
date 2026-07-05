using System.Text.Json.Serialization;

namespace KrogerShopperMcp.Models;

internal sealed class KrogerCommandRequest
{
    public string? Command { get; init; }
    public string? State { get; init; }
    public string[]? Scopes { get; init; }
    public string? Query { get; init; }
    public string? LocationId { get; init; }
    public int? Limit { get; init; }
    public string? ZipCode { get; init; }
    public string? Chain { get; init; }
    public string? Name { get; init; }
    public string? Label { get; init; }
    public string? Upc { get; init; }
    public string? Identifier { get; init; }
    public int? Quantity { get; init; }
    public string? Modality { get; init; }
    public bool? DryRun { get; init; }
    [JsonPropertyName("dry_run")]
    public bool? DryRunAlias { get; init; }
    public bool? AllowUnknownStock { get; init; }
    [JsonPropertyName("allow_unknown_stock")]
    public bool? AllowUnknownStockAlias { get; init; }

    public bool IsDryRun => DryRun == true || DryRunAlias == true;
    public bool IsAllowUnknownStock => AllowUnknownStock == true || AllowUnknownStockAlias == true;
}

internal sealed class KrogerSetStoreRequest
{
    public string? LocationId { get; init; }
    public string? Label { get; init; }
}

internal sealed class KrogerAddToCartRequest
{
    public string? Upc { get; init; }
    public int Quantity { get; init; }
    public string? Modality { get; init; }
    public bool? DryRun { get; init; }
    [JsonPropertyName("dry_run")]
    public bool? DryRunAlias { get; init; }
    public bool? AllowUnknownStock { get; init; }
    [JsonPropertyName("allow_unknown_stock")]
    public bool? AllowUnknownStockAlias { get; init; }

    public bool IsDryRun => DryRun == true || DryRunAlias == true;
    public bool IsAllowUnknownStock => AllowUnknownStock == true || AllowUnknownStockAlias == true;
}

internal sealed class KrogerRemoveTrackedCartItemRequest
{
    public string? Upc { get; init; }
    public int? Quantity { get; init; }
}

internal sealed class KrogerSetTrackedCartItemQuantityRequest
{
    public string? Upc { get; init; }
    public int Quantity { get; init; }
}

internal sealed class KrogerMarkPurchasedRequest
{
    public string? Upc { get; init; }
    public int? Quantity { get; init; }
    public string? PurchasedAtUtc { get; init; }
}

internal sealed class KrogerSendTrackedCartRequest
{
    public bool? AllowUnknownStock { get; init; }
    [JsonPropertyName("allow_unknown_stock")]
    public bool? AllowUnknownStockAlias { get; init; }
    public bool IsAllowUnknownStock => AllowUnknownStock == true || AllowUnknownStockAlias == true;
}

internal sealed class KrogerSaveCartRequest
{
    public string? Name { get; init; }
}

internal sealed class KrogerRenameSavedCartRequest
{
    public string? Name { get; init; }
    public string? NewName { get; init; }
}

internal sealed class KrogerDuplicateSavedCartRequest
{
    public string? Name { get; init; }
    public string? NewName { get; init; }
}

internal sealed class KrogerDeleteSavedCartRequest
{
    public string? Name { get; init; }
}

internal sealed class KrogerSetSavedCartItemQuantityRequest
{
    public string? Name { get; init; }
    public string? Upc { get; init; }
    public int Quantity { get; init; }
}

internal sealed class KrogerAddSavedCartItemRequest
{
    public string? Name { get; init; }
    public string? Identifier { get; init; }
    public int Quantity { get; init; }
}

internal sealed class KrogerAddTrackedCartItemRequest
{
    public string? Identifier { get; init; }
    public int Quantity { get; init; }
}

internal sealed class KrogerRemoveSavedCartItemRequest
{
    public string? Name { get; init; }
    public string? Upc { get; init; }
}

internal sealed class KrogerApplySavedCartRequest
{
    public string? Name { get; init; }
    public bool? DryRun { get; init; }
    [JsonPropertyName("dry_run")]
    public bool? DryRunAlias { get; init; }
    public bool? AllowUnknownStock { get; init; }
    [JsonPropertyName("allow_unknown_stock")]
    public bool? AllowUnknownStockAlias { get; init; }

    public bool IsDryRun => DryRun == true || DryRunAlias == true;
    public bool IsAllowUnknownStock => AllowUnknownStock == true || AllowUnknownStockAlias == true;
}

internal sealed class KrogerAddToStagedCartRequest
{
    public string? Upc { get; init; }
    public int Quantity { get; init; }
}

internal sealed class KrogerRemoveStagedCartItemRequest
{
    public string? Upc { get; init; }
    public int? Quantity { get; init; }
}

internal sealed class KrogerSaveStagedCartRequest
{
    public string? Name { get; init; }
}

internal sealed class KrogerLoadSavedCartToStagedRequest
{
    public string? Name { get; init; }
    public bool? ReplaceExisting { get; init; }

    public bool IsReplaceExisting => ReplaceExisting == true;
}

internal sealed class KrogerCommitStagedCartRequest
{
    public bool? DryRun { get; init; }
    [JsonPropertyName("dry_run")]
    public bool? DryRunAlias { get; init; }
    public bool? AllowUnknownStock { get; init; }
    [JsonPropertyName("allow_unknown_stock")]
    public bool? AllowUnknownStockAlias { get; init; }
    public bool? ClearOnSuccess { get; init; }

    public bool IsDryRun => DryRun == true || DryRunAlias == true;
    public bool IsAllowUnknownStock => AllowUnknownStock == true || AllowUnknownStockAlias == true;
    public bool IsClearOnSuccess => ClearOnSuccess != false;
}
