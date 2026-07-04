using KrogerShopperMcp.Models;
using Microsoft.Data.Sqlite;

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
              updated_at_utc text not null
            );
            """;
        await cmd.ExecuteNonQueryAsync();
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
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            insert into tracked_cart_items (upc, quantity, updated_at_utc)
            values ($upc, $quantity, $updated)
            on conflict(upc) do update set
              quantity = tracked_cart_items.quantity + excluded.quantity,
              updated_at_utc = excluded.updated_at_utc
            """;
        cmd.Parameters.AddWithValue("$upc", upc);
        cmd.Parameters.AddWithValue("$quantity", quantity);
        cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<TrackedCartItem>> GetTrackedCartItemsAsync()
    {
        await using var db = await OpenDbAsync();
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            select upc, quantity, updated_at_utc
            from tracked_cart_items
            order by updated_at_utc desc
            """;

        var items = new List<TrackedCartItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new TrackedCartItem(
                reader.GetString(0),
                reader.GetInt32(1),
                DateTimeOffset.Parse(reader.GetString(2))));
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
}
