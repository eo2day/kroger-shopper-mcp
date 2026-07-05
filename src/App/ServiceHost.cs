using KrogerShopperMcp.Api;
using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KrogerShopperMcp.App;

internal static class ServiceHost
{
    public static async Task<int> RunAsync(KrogerConfig config, IReadOnlyList<string> defaultScopes)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(config.ServiceUrl);
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(new KrogerStore(config.DbPath));
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton(sp =>
            new KrogerOAuthClient(
                config,
                defaultScopes,
                sp.GetRequiredService<ILogger<KrogerOAuthClient>>()));
        builder.Services.AddSingleton<KrogerProductsClient>();
        builder.Services.AddSingleton<KrogerLocationsClient>();
        builder.Services.AddSingleton<KrogerCartService>();
        builder.Services.AddSingleton<KrogerWebAuthService>();

        var app = builder.Build();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("KrogerAssistant");
        var store = app.Services.GetRequiredService<KrogerStore>();
        await store.InitializeAsync();
        logger.LogInformation("Kroger Assistant starting on {ServiceUrl} with db {DbPath}", config.ServiceUrl, config.DbPath);

        app.MapKrogerEndpoints();

        await app.RunAsync();
        return 0;
    }
}
