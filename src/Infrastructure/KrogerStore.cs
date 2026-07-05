using KrogerShopperMcp.Models;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace KrogerShopperMcp.Infrastructure;

internal sealed class KrogerStore
{
    private readonly string _dbPath;

    public KrogerStore(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task InitializeAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            create table if not exists oauth_tokens (
              provider text primary key,
              access_token text not null,
              refresh_token text null,
              token_type text not null,
              scope text not null,
              expires_at_utc text not null,
              created_at_utc text not null,
              updated_at_utc text not null
            );

            create table if not exists oauth_states (
              state text primary key,
              scope text not null,
              created_at_utc text not null
            );

            create table if not exists app_settings (
              setting_key text primary key,
              setting_value text not null,
              updated_at_utc text not null
            );

            create table if not exists tracked_cart_items (
              upc text primary key,
              quantity integer not null,
              created_at_utc text not null,
              updated_at_utc text not null
            );

            create table if not exists staged_cart_items (
              upc text primary key,
              quantity integer not null,
              created_at_utc text not null,
              updated_at_utc text not null
            );

            create table if not exists purchased_items (
              id integer primary key autoincrement,
              upc text not null,
              quantity integer not null,
              purchased_at_utc text not null
            );

            create table if not exists saved_carts (
              name text primary key,
              items_json text not null,
              created_at_utc text not null,
              updated_at_utc text not null
            );

            create table if not exists web_credentials (
              username text primary key,
              password_hash text not null,
              password_salt text not null,
              password_iterations integer not null,
              created_at_utc text not null,
              updated_at_utc text not null
            );

            create table if not exists web_sessions (
              session_id text primary key,
              username text not null,
              expires_at_utc text not null,
              created_at_utc text not null
            );
            """;
        await cmd.ExecuteNonQueryAsync();

        await EnsureCreatedAtColumnAsync(db, "tracked_cart_items");
        await EnsureCreatedAtColumnAsync(db, "staged_cart_items");
    }

    public async Task SavePendingStateAsync(string state, IReadOnlyList<string> scopes)
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into oauth_states (state, scope, created_at_utc)
            values ($state, $scope, $created)
            on conflict(state) do update set
              scope = excluded.scope,
              created_at_utc = excluded.created_at_utc
            """;
        cmd.Parameters.AddWithValue("$state", state);
        cmd.Parameters.AddWithValue("$scope", string.Join(' ', scopes));
        cmd.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetScopeForStateAsync(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = "select scope from oauth_states where state = $state limit 1";
        cmd.Parameters.AddWithValue("$state", state);
        return await cmd.ExecuteScalarAsync() as string;
    }

    public async Task UpsertTokenAsync(KrogerTokenResponse token, string scope)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddSeconds(token.ExpiresIn);

        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into oauth_tokens
            (
              provider, access_token, refresh_token, token_type, scope,
              expires_at_utc, created_at_utc, updated_at_utc
            )
            values
            (
              'kroger', $access, $refresh, $tokenType, $scope,
              $expiresAt, $createdAt, $updatedAt
            )
            on conflict(provider) do update set
              access_token = excluded.access_token,
              refresh_token = excluded.refresh_token,
              token_type = excluded.token_type,
              scope = excluded.scope,
              expires_at_utc = excluded.expires_at_utc,
              updated_at_utc = excluded.updated_at_utc
            """;
        cmd.Parameters.AddWithValue("$access", token.AccessToken);
        cmd.Parameters.AddWithValue("$refresh", (object?)token.RefreshToken ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tokenType", token.TokenType);
        cmd.Parameters.AddWithValue("$scope", string.IsNullOrWhiteSpace(token.Scope) ? scope : token.Scope);
        cmd.Parameters.AddWithValue("$expiresAt", expiresAt.ToString("O"));
        cmd.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<TokenSummary?> GetTokenSummaryAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select scope, token_type, expires_at_utc, created_at_utc, updated_at_utc,
                   length(access_token), length(refresh_token)
            from oauth_tokens
            where provider = 'kroger'
            limit 1
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new TokenSummary(
            reader.GetString(0),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4)),
            reader.GetInt32(5),
            reader.GetInt32(6));
    }

    public async Task<StoredToken?> GetStoredTokenAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select access_token, refresh_token, scope, token_type, expires_at_utc
            from oauth_tokens
            where provider = 'kroger'
            limit 1
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new StoredToken(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4)));
    }

    public async Task<string?> GetDefaultStoreIdAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = "select setting_value from app_settings where setting_key = 'default_store_id' limit 1";
        return await cmd.ExecuteScalarAsync() as string;
    }

    public async Task SetDefaultStoreAsync(string locationId, string? label)
    {
        await using var db = await OpenDbAsync();

        var storeId = db.CreateCommand();
        storeId.CommandText = """
            insert into app_settings (setting_key, setting_value, updated_at_utc)
            values ('default_store_id', $value, $updated)
            on conflict(setting_key) do update set
              setting_value = excluded.setting_value,
              updated_at_utc = excluded.updated_at_utc
            """;
        storeId.Parameters.AddWithValue("$value", locationId);
        storeId.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await storeId.ExecuteNonQueryAsync();

        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var labelCmd = db.CreateCommand();
        labelCmd.CommandText = """
            insert into app_settings (setting_key, setting_value, updated_at_utc)
            values ('default_store_label', $value, $updated)
            on conflict(setting_key) do update set
              setting_value = excluded.setting_value,
              updated_at_utc = excluded.updated_at_utc
            """;
        labelCmd.Parameters.AddWithValue("$value", label);
        labelCmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await labelCmd.ExecuteNonQueryAsync();
    }

    public async Task AddTrackedCartItemAsync(string upc, int quantity)
    {
        await using var db = await OpenDbAsync();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into tracked_cart_items (upc, quantity, created_at_utc, updated_at_utc)
            values ($upc, $quantity, $created, $updated)
            on conflict(upc) do update set
              quantity = tracked_cart_items.quantity + excluded.quantity,
              updated_at_utc = excluded.updated_at_utc
            """;
        cmd.Parameters.AddWithValue("$upc", upc);
        cmd.Parameters.AddWithValue("$quantity", quantity);
        cmd.Parameters.AddWithValue("$created", now);
        cmd.Parameters.AddWithValue("$updated", now);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> SetTrackedCartItemQuantityAsync(string upc, int quantity)
    {
        await using var db = await OpenDbAsync();

        if (quantity <= 0)
        {
            var deleteCmd = db.CreateCommand();
            deleteCmd.CommandText = "delete from tracked_cart_items where upc = $upc";
            deleteCmd.Parameters.AddWithValue("$upc", upc);
            await deleteCmd.ExecuteNonQueryAsync();
            return 0;
        }

        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into tracked_cart_items (upc, quantity, created_at_utc, updated_at_utc)
            values ($upc, $quantity, $created, $updated)
            on conflict(upc) do update set
              quantity = excluded.quantity,
              updated_at_utc = excluded.updated_at_utc
            """;
        cmd.Parameters.AddWithValue("$upc", upc);
        cmd.Parameters.AddWithValue("$quantity", quantity);
        var now = DateTimeOffset.UtcNow.ToString("O");
        cmd.Parameters.AddWithValue("$created", now);
        cmd.Parameters.AddWithValue("$updated", now);
        await cmd.ExecuteNonQueryAsync();
        return quantity;
    }

    public async Task AddStagedCartItemAsync(string upc, int quantity)
    {
        await using var db = await OpenDbAsync();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into staged_cart_items (upc, quantity, created_at_utc, updated_at_utc)
            values ($upc, $quantity, $created, $updated)
            on conflict(upc) do update set
              quantity = staged_cart_items.quantity + excluded.quantity,
              updated_at_utc = excluded.updated_at_utc
            """;
        cmd.Parameters.AddWithValue("$upc", upc);
        cmd.Parameters.AddWithValue("$quantity", quantity);
        cmd.Parameters.AddWithValue("$created", now);
        cmd.Parameters.AddWithValue("$updated", now);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<TrackedCartItem>> GetTrackedCartItemsAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select upc, quantity, created_at_utc, updated_at_utc
            from tracked_cart_items
            order by created_at_utc asc, upc asc
            """;

        var items = new List<TrackedCartItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new TrackedCartItem(
                reader.GetString(0),
                reader.GetInt32(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                DateTimeOffset.Parse(reader.GetString(3))));
        }

        return items;
    }

    public async Task<IReadOnlyList<StagedCartItem>> GetStagedCartItemsAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select upc, quantity, created_at_utc, updated_at_utc
            from staged_cart_items
            order by created_at_utc asc, upc asc
            """;

        var items = new List<StagedCartItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new StagedCartItem(
                reader.GetString(0),
                reader.GetInt32(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                DateTimeOffset.Parse(reader.GetString(3))));
        }

        return items;
    }

    public async Task<TrackedCartRemovalResult> RemoveTrackedCartItemAsync(string upc, int? quantity)
    {
        await using var db = await OpenDbAsync();

        var getCmd = db.CreateCommand();
        getCmd.CommandText = """
            select quantity
            from tracked_cart_items
            where upc = $upc
            limit 1
            """;
        getCmd.Parameters.AddWithValue("$upc", upc);
        var currentObj = await getCmd.ExecuteScalarAsync();
        var currentQuantity = currentObj is long currentLong ? (int)currentLong : 0;

        if (currentQuantity <= 0)
        {
            return new TrackedCartRemovalResult(0, 0);
        }

        var removeQuantity = quantity is > 0 ? quantity.Value : currentQuantity;
        var remainingQuantity = Math.Max(0, currentQuantity - removeQuantity);

        if (remainingQuantity == 0)
        {
            var deleteCmd = db.CreateCommand();
            deleteCmd.CommandText = "delete from tracked_cart_items where upc = $upc";
            deleteCmd.Parameters.AddWithValue("$upc", upc);
            await deleteCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var updateCmd = db.CreateCommand();
            updateCmd.CommandText = """
                update tracked_cart_items
                set quantity = $quantity,
                    updated_at_utc = $updated
                where upc = $upc
                """;
            updateCmd.Parameters.AddWithValue("$upc", upc);
            updateCmd.Parameters.AddWithValue("$quantity", remainingQuantity);
            updateCmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            await updateCmd.ExecuteNonQueryAsync();
        }

        return new TrackedCartRemovalResult(Math.Min(removeQuantity, currentQuantity), remainingQuantity);
    }

    public async Task<TrackedCartRemovalResult> RemoveStagedCartItemAsync(string upc, int? quantity)
    {
        await using var db = await OpenDbAsync();

        var getCmd = db.CreateCommand();
        getCmd.CommandText = """
            select quantity
            from staged_cart_items
            where upc = $upc
            limit 1
            """;
        getCmd.Parameters.AddWithValue("$upc", upc);
        var currentObj = await getCmd.ExecuteScalarAsync();
        var currentQuantity = currentObj is long currentLong ? (int)currentLong : 0;

        if (currentQuantity <= 0)
        {
            return new TrackedCartRemovalResult(0, 0);
        }

        var removeQuantity = quantity is > 0 ? quantity.Value : currentQuantity;
        var remainingQuantity = Math.Max(0, currentQuantity - removeQuantity);

        if (remainingQuantity == 0)
        {
            var deleteCmd = db.CreateCommand();
            deleteCmd.CommandText = "delete from staged_cart_items where upc = $upc";
            deleteCmd.Parameters.AddWithValue("$upc", upc);
            await deleteCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var updateCmd = db.CreateCommand();
            updateCmd.CommandText = """
                update staged_cart_items
                set quantity = $quantity,
                    updated_at_utc = $updated
                where upc = $upc
                """;
            updateCmd.Parameters.AddWithValue("$upc", upc);
            updateCmd.Parameters.AddWithValue("$quantity", remainingQuantity);
            updateCmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            await updateCmd.ExecuteNonQueryAsync();
        }

        return new TrackedCartRemovalResult(Math.Min(removeQuantity, currentQuantity), remainingQuantity);
    }

    public async Task<PurchasedItem?> MarkTrackedCartItemPurchasedAsync(string upc, int? quantity, DateTimeOffset? purchasedAtUtc = null)
    {
        await using var db = await OpenDbAsync();

        var getCmd = db.CreateCommand();
        getCmd.CommandText = """
            select quantity
            from tracked_cart_items
            where upc = $upc
            limit 1
            """;
        getCmd.Parameters.AddWithValue("$upc", upc);
        var currentObj = await getCmd.ExecuteScalarAsync();
        var currentQuantity = currentObj is long currentLong ? (int)currentLong : 0;

        if (currentQuantity <= 0)
        {
            return null;
        }

        var purchasedQuantity = quantity is > 0 ? Math.Min(quantity.Value, currentQuantity) : currentQuantity;
        var purchasedAt = purchasedAtUtc ?? DateTimeOffset.UtcNow;

        var insertCmd = db.CreateCommand();
        insertCmd.CommandText = """
            insert into purchased_items (upc, quantity, purchased_at_utc)
            values ($upc, $quantity, $purchasedAt)
            returning id
            """;
        insertCmd.Parameters.AddWithValue("$upc", upc);
        insertCmd.Parameters.AddWithValue("$quantity", purchasedQuantity);
        insertCmd.Parameters.AddWithValue("$purchasedAt", purchasedAt.ToString("O"));
        var idObj = await insertCmd.ExecuteScalarAsync();
        var id = idObj is long longId ? longId : Convert.ToInt64(idObj);

        var remainingQuantity = currentQuantity - purchasedQuantity;
        if (remainingQuantity <= 0)
        {
            var deleteCmd = db.CreateCommand();
            deleteCmd.CommandText = "delete from tracked_cart_items where upc = $upc";
            deleteCmd.Parameters.AddWithValue("$upc", upc);
            await deleteCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var updateCmd = db.CreateCommand();
            updateCmd.CommandText = """
                update tracked_cart_items
                set quantity = $quantity,
                    updated_at_utc = $updated
                where upc = $upc
                """;
            updateCmd.Parameters.AddWithValue("$upc", upc);
            updateCmd.Parameters.AddWithValue("$quantity", remainingQuantity);
            updateCmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            await updateCmd.ExecuteNonQueryAsync();
        }

        return new PurchasedItem(id, upc, purchasedQuantity, purchasedAt);
    }

    public async Task<IReadOnlyList<PurchasedItem>> MarkAllTrackedCartItemsPurchasedAsync(DateTimeOffset? purchasedAtUtc = null)
    {
        var trackedItems = await GetTrackedCartItemsAsync();
        var purchasedAt = purchasedAtUtc ?? DateTimeOffset.UtcNow;
        var purchasedItems = new List<PurchasedItem>();

        foreach (var trackedItem in trackedItems)
        {
            var purchasedItem = await MarkTrackedCartItemPurchasedAsync(trackedItem.Upc, trackedItem.Quantity, purchasedAt);
            if (purchasedItem is not null)
            {
                purchasedItems.Add(purchasedItem);
            }
        }

        return purchasedItems;
    }

    public async Task<int> ClearTrackedCartAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = "delete from tracked_cart_items";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> ClearStagedCartAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = "delete from staged_cart_items";
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<PurchasedItem>> GetPurchasedItemsAsync(int limit = 100)
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select id, upc, quantity, purchased_at_utc
            from purchased_items
            order by purchased_at_utc desc, id desc
            limit $limit
            """;
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));

        var items = new List<PurchasedItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new PurchasedItem(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                DateTimeOffset.Parse(reader.GetString(3))));
        }

        return items;
    }

    public async Task<SavedCart> SaveTrackedCartAsync(string name)
    {
        var trackedItems = await GetTrackedCartItemsAsync();
        return await SaveCartItemsAsync(name, trackedItems.Select(item => (item.Upc, item.Quantity)));
    }

    public async Task<SavedCart> SaveStagedCartAsync(string name)
    {
        var stagedItems = await GetStagedCartItemsAsync();
        return await SaveCartItemsAsync(name, stagedItems.Select(item => (item.Upc, item.Quantity)));
    }

    private async Task<SavedCart> SaveCartItemsAsync(string name, IEnumerable<(string Upc, int Quantity)> items)
    {
        var itemsJson = JsonSerializer.Serialize(items.Select(item => new
        {
            upc = item.Upc,
            quantity = item.Quantity
        }));

        await using var db = await OpenDbAsync();
        var now = DateTimeOffset.UtcNow;
        var existingCreatedAtCmd = db.CreateCommand();
        existingCreatedAtCmd.CommandText = "select created_at_utc from saved_carts where name = $name limit 1";
        existingCreatedAtCmd.Parameters.AddWithValue("$name", name);
        var createdAtRaw = await existingCreatedAtCmd.ExecuteScalarAsync() as string;
        var createdAt = createdAtRaw is not null ? DateTimeOffset.Parse(createdAtRaw) : now;

        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into saved_carts (name, items_json, created_at_utc, updated_at_utc)
            values ($name, $itemsJson, $createdAt, $updatedAt)
            on conflict(name) do update set
              items_json = excluded.items_json,
              updated_at_utc = excluded.updated_at_utc
            """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$itemsJson", itemsJson);
        cmd.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync();

        return new SavedCart(name, itemsJson, createdAt, now);
    }

    public async Task<int> LoadSavedCartIntoStagedAsync(string name, bool replaceExisting)
    {
        var savedCart = await GetSavedCartAsync(name);
        if (savedCart is null)
        {
            return -1;
        }

        var items = JsonSerializer.Deserialize<List<SavedCartItem>>(savedCart.ItemsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        if (replaceExisting)
        {
            await ClearStagedCartAsync();
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Upc) || item.Quantity <= 0)
            {
                continue;
            }

            await AddStagedCartItemAsync(item.Upc.Trim(), item.Quantity);
        }

        return items.Count;
    }

    private sealed record SavedCartItem(string? Upc, int Quantity);

    public async Task<SavedCart?> SetSavedCartItemQuantityAsync(string name, string upc, int quantity)
    {
        var savedCart = await GetSavedCartAsync(name);
        if (savedCart is null)
        {
            return null;
        }

        var items = JsonSerializer.Deserialize<List<SavedCartItem>>(savedCart.ItemsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        var normalizedUpc = upc.Trim();
        var updatedItems = new List<SavedCartItem>();
        var found = false;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Upc))
            {
                continue;
            }

            var itemUpc = item.Upc.Trim();
            if (itemUpc.Equals(normalizedUpc, StringComparison.Ordinal))
            {
                found = true;
                if (quantity > 0)
                {
                    updatedItems.Add(new SavedCartItem(itemUpc, quantity));
                }

                continue;
            }

            if (item.Quantity > 0)
            {
                updatedItems.Add(new SavedCartItem(itemUpc, item.Quantity));
            }
        }

        if (!found)
        {
            if (quantity > 0)
            {
                updatedItems.Add(new SavedCartItem(normalizedUpc, quantity));
            }
        }

        return await SaveCartItemsAsync(name, updatedItems.Select(item => (item.Upc!, item.Quantity)));
    }

    public async Task<SavedCart?> AddSavedCartItemAsync(string name, string upc, int quantity)
    {
        var savedCart = await GetSavedCartAsync(name);
        if (savedCart is null)
        {
            return null;
        }

        var items = JsonSerializer.Deserialize<List<SavedCartItem>>(savedCart.ItemsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        var normalizedUpc = upc.Trim();
        var updatedItems = new List<SavedCartItem>();
        var found = false;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Upc))
            {
                continue;
            }

            var itemUpc = item.Upc.Trim();
            if (itemUpc.Equals(normalizedUpc, StringComparison.Ordinal))
            {
                found = true;
                updatedItems.Add(new SavedCartItem(itemUpc, Math.Max(0, item.Quantity) + quantity));
            }
            else if (item.Quantity > 0)
            {
                updatedItems.Add(new SavedCartItem(itemUpc, item.Quantity));
            }
        }

        if (!found && quantity > 0)
        {
            updatedItems.Add(new SavedCartItem(normalizedUpc, quantity));
        }

        return await SaveCartItemsAsync(name, updatedItems.Select(item => (item.Upc!, item.Quantity)));
    }

    public async Task<(SavedCart? Cart, bool Removed)> RemoveSavedCartItemAsync(string name, string upc)
    {
        var savedCart = await GetSavedCartAsync(name);
        if (savedCart is null)
        {
            return (null, false);
        }

        var items = JsonSerializer.Deserialize<List<SavedCartItem>>(savedCart.ItemsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        var normalizedUpc = upc.Trim();
        var removed = false;
        var updatedItems = new List<SavedCartItem>();

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Upc))
            {
                continue;
            }

            var itemUpc = item.Upc.Trim();
            if (itemUpc.Equals(normalizedUpc, StringComparison.Ordinal))
            {
                removed = true;
                continue;
            }

            if (item.Quantity > 0)
            {
                updatedItems.Add(new SavedCartItem(itemUpc, item.Quantity));
            }
        }

        var updatedCart = await SaveCartItemsAsync(name, updatedItems.Select(item => (item.Upc!, item.Quantity)));
        return (updatedCart, removed);
    }

    public async Task<SavedCart?> RenameSavedCartAsync(string name, string newName)
    {
        var savedCart = await GetSavedCartAsync(name);
        if (savedCart is null)
        {
            return null;
        }

        var normalizedName = name.Trim();
        var normalizedNewName = newName.Trim();
        if (string.Equals(normalizedName, normalizedNewName, StringComparison.Ordinal))
        {
            return new SavedCart(normalizedNewName, savedCart.ItemsJson, savedCart.CreatedAtUtc, DateTimeOffset.UtcNow);
        }

        await using var db = await OpenDbAsync();
        var now = DateTimeOffset.UtcNow;

        var upsertCmd = db.CreateCommand();
        upsertCmd.CommandText = """
            insert into saved_carts (name, items_json, created_at_utc, updated_at_utc)
            values ($name, $itemsJson, $createdAt, $updatedAt)
            on conflict(name) do update set
              items_json = excluded.items_json,
              updated_at_utc = excluded.updated_at_utc
            """;
        upsertCmd.Parameters.AddWithValue("$name", normalizedNewName);
        upsertCmd.Parameters.AddWithValue("$itemsJson", savedCart.ItemsJson);
        upsertCmd.Parameters.AddWithValue("$createdAt", savedCart.CreatedAtUtc.ToString("O"));
        upsertCmd.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        await upsertCmd.ExecuteNonQueryAsync();

        var deleteCmd = db.CreateCommand();
        deleteCmd.CommandText = "delete from saved_carts where name = $name";
        deleteCmd.Parameters.AddWithValue("$name", normalizedName);
        await deleteCmd.ExecuteNonQueryAsync();

        return new SavedCart(normalizedNewName, savedCart.ItemsJson, savedCart.CreatedAtUtc, now);
    }

    public async Task<SavedCart?> DuplicateSavedCartAsync(string name, string newName)
    {
        var savedCart = await GetSavedCartAsync(name);
        if (savedCart is null)
        {
            return null;
        }

        var items = JsonSerializer.Deserialize<List<SavedCartItem>>(savedCart.ItemsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        return await SaveCartItemsAsync(
            newName.Trim(),
            items
                .Where(static item => !string.IsNullOrWhiteSpace(item.Upc) && item.Quantity > 0)
                .Select(item => (item.Upc!.Trim(), item.Quantity)));
    }

    public async Task<bool> DeleteSavedCartAsync(string name)
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = "delete from saved_carts where name = $name";
        cmd.Parameters.AddWithValue("$name", name.Trim());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<SavedCart?> GetSavedCartAsync(string name)
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select name, items_json, created_at_utc, updated_at_utc
            from saved_carts
            where name = $name
            limit 1
            """;
        cmd.Parameters.AddWithValue("$name", name);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new SavedCart(
            reader.GetString(0),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3)));
    }

    public async Task<WebCredential?> GetWebCredentialAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select username, password_hash, password_salt, password_iterations, created_at_utc, updated_at_utc
            from web_credentials
            order by created_at_utc asc
            limit 1
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WebCredential(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            DateTimeOffset.Parse(reader.GetString(4)),
            DateTimeOffset.Parse(reader.GetString(5)));
    }

    public async Task UpsertWebCredentialAsync(string username, string passwordHash, string passwordSalt, int passwordIterations)
    {
        await using var db = await OpenDbAsync();
        var now = DateTimeOffset.UtcNow;
        var createdAt = (await GetWebCredentialAsync())?.CreatedAtUtc ?? now;

        var clearCmd = db.CreateCommand();
        clearCmd.CommandText = "delete from web_credentials";
        await clearCmd.ExecuteNonQueryAsync();

        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into web_credentials (
              username, password_hash, password_salt, password_iterations, created_at_utc, updated_at_utc
            )
            values ($username, $passwordHash, $passwordSalt, $passwordIterations, $createdAt, $updatedAt)
            """;
        cmd.Parameters.AddWithValue("$username", username);
        cmd.Parameters.AddWithValue("$passwordHash", passwordHash);
        cmd.Parameters.AddWithValue("$passwordSalt", passwordSalt);
        cmd.Parameters.AddWithValue("$passwordIterations", passwordIterations);
        cmd.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CreateWebSessionAsync(string sessionId, string username, DateTimeOffset expiresAtUtc)
    {
        await using var db = await OpenDbAsync();
        var now = DateTimeOffset.UtcNow;
        var cleanupCmd = db.CreateCommand();
        cleanupCmd.CommandText = "delete from web_sessions where expires_at_utc <= $now";
        cleanupCmd.Parameters.AddWithValue("$now", now.ToString("O"));
        await cleanupCmd.ExecuteNonQueryAsync();

        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into web_sessions (session_id, username, expires_at_utc, created_at_utc)
            values ($sessionId, $username, $expiresAtUtc, $createdAtUtc)
            """;
        cmd.Parameters.AddWithValue("$sessionId", sessionId);
        cmd.Parameters.AddWithValue("$username", username);
        cmd.Parameters.AddWithValue("$expiresAtUtc", expiresAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$createdAtUtc", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<WebSession?> GetWebSessionAsync(string sessionId)
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select session_id, username, expires_at_utc, created_at_utc
            from web_sessions
            where session_id = $sessionId
            limit 1
            """;
        cmd.Parameters.AddWithValue("$sessionId", sessionId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WebSession(
            reader.GetString(0),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3)));
    }

    public async Task DeleteWebSessionAsync(string sessionId)
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = "delete from web_sessions where session_id = $sessionId";
        cmd.Parameters.AddWithValue("$sessionId", sessionId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<SavedCart>> GetSavedCartsAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select name, items_json, created_at_utc, updated_at_utc
            from saved_carts
            order by updated_at_utc desc, name asc
            """;

        var carts = new List<SavedCart>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            carts.Add(new SavedCart(
                reader.GetString(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                DateTimeOffset.Parse(reader.GetString(3))));
        }

        return carts;
    }

    private async Task<SqliteConnection> OpenDbAsync()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        FilePermissionHelper.TryHardenOwnerOnly(_dbPath);
        return connection;
    }

    private static async Task EnsureCreatedAtColumnAsync(SqliteConnection db, string tableName)
    {
        var pragmaCmd = db.CreateCommand();
        pragmaCmd.CommandText = $"pragma table_info({tableName})";

        var hasCreatedAt = false;
        await using (var reader = await pragmaCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), "created_at_utc", StringComparison.OrdinalIgnoreCase))
                {
                    hasCreatedAt = true;
                    break;
                }
            }
        }

        if (!hasCreatedAt)
        {
            var alterCmd = db.CreateCommand();
            alterCmd.CommandText = $"alter table {tableName} add column created_at_utc text null";
            await alterCmd.ExecuteNonQueryAsync();
        }

        var backfillCmd = db.CreateCommand();
        backfillCmd.CommandText = $"update {tableName} set created_at_utc = updated_at_utc where created_at_utc is null or trim(created_at_utc) = ''";
        await backfillCmd.ExecuteNonQueryAsync();
    }
}
