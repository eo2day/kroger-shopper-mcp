using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Services;
using KrogerShopperMcp.Utilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace KrogerShopperMcp.App;

internal sealed class KrogerApplication
{
    private static readonly string[] DefaultScopes = ["product.compact", "cart.basic:write"];

    public async Task<int> RunAsync(string[] args)
    {
        try
        {
            var config = KrogerConfig.LoadFromEnvironment();

            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            return args[0] switch
            {
                "serve" => await ServiceHost.RunAsync(config, DefaultScopes),
                "init-db" => await InitDbAsync(config),
                "auth-url" => await PrintAuthUrlAsync(config, args.Skip(1).ToArray()),
                "exchange-code" => await ExchangeCodeAsync(config, args.Skip(1).ToArray()),
                "show-token" => await ShowTokenAsync(config),
                "refresh-token" => await RefreshTokenAsync(config),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> InitDbAsync(KrogerConfig config)
    {
        var store = new KrogerStore(config.DbPath);
        await store.InitializeAsync();
        Console.WriteLine(config.DbPath);
        return 0;
    }

    private static async Task<int> PrintAuthUrlAsync(KrogerConfig config, string[] args)
    {
        var state = CommandLineOptions.GetOptionValue(args, "--state")
                    ?? $"kroger-shopper-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var scopes = ScopeParser.ParseScopes(
            CommandLineOptions.GetOptionValue(args, "--scopes"),
            DefaultScopes);

        var store = new KrogerStore(config.DbPath);
        var oauthClient = new KrogerOAuthClient(config, DefaultScopes, NullLogger<KrogerOAuthClient>.Instance);
        await store.InitializeAsync();
        await store.SavePendingStateAsync(state, scopes);

        Console.WriteLine(oauthClient.BuildAuthorizeUrl(state, scopes));
        return 0;
    }

    private static async Task<int> ExchangeCodeAsync(KrogerConfig config, string[] args)
    {
        var code = CommandLineOptions.GetRequiredOptionValue(args, "--code");
        var state = CommandLineOptions.GetOptionValue(args, "--state");
        var scopes = ScopeParser.ParseScopes(
            CommandLineOptions.GetOptionValue(args, "--scopes"),
            DefaultScopes);

        var store = new KrogerStore(config.DbPath);
        var oauthClient = new KrogerOAuthClient(config, DefaultScopes, NullLogger<KrogerOAuthClient>.Instance);
        await store.InitializeAsync();

        await oauthClient.ExchangeAuthorizationCodeAsync(store, code, state, string.Join(' ', scopes));

        Console.WriteLine("token exchange succeeded");
        return 0;
    }

    private static async Task<int> ShowTokenAsync(KrogerConfig config)
    {
        var store = new KrogerStore(config.DbPath);
        await store.InitializeAsync();
        var status = await store.GetTokenSummaryAsync();

        if (status is null)
        {
            Console.WriteLine("no token stored");
            return 0;
        }

        Console.WriteLine(JsonDefaults.SerializeIndented(new
        {
            scope = status.Scope,
            token_type = status.TokenType,
            expires_at_utc = status.ExpiresAtUtc.ToString("O"),
            created_at_utc = status.CreatedAtUtc.ToString("O"),
            updated_at_utc = status.UpdatedAtUtc.ToString("O"),
            access_token_length = status.AccessTokenLength,
            refresh_token_length = status.RefreshTokenLength
        }));

        return 0;
    }

    private static async Task<int> RefreshTokenAsync(KrogerConfig config)
    {
        var store = new KrogerStore(config.DbPath);
        var oauthClient = new KrogerOAuthClient(config, DefaultScopes, NullLogger<KrogerOAuthClient>.Instance);
        await store.InitializeAsync();
        var token = await oauthClient.RefreshTokenAsync(store);

        Console.WriteLine("refresh succeeded");
        Console.WriteLine($"expires_in: {token.ExpiresIn}");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            KrogerCs

            Commands:
              serve
              init-db
              auth-url [--state VALUE] [--scopes scope1,scope2]
              exchange-code --code VALUE [--state VALUE] [--scopes scope1,scope2]
              show-token
              refresh-token
            """);
    }
}
