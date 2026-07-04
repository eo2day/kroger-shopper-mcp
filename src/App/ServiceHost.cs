using KrogerShopperMcp.Api;
using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Infrastructure;
using KrogerShopperMcp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace KrogerShopperMcp.App;

internal static class ServiceHost
{
    public static async Task<int> RunAsync(KrogerConfig config, IReadOnlyList<string> defaultScopes)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(config.ServiceUrl);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(new KrogerStore(config.DbPath));
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton(_ => new KrogerOAuthClient(config, defaultScopes));
        builder.Services.AddSingleton<KrogerProductsClient>();
        builder.Services.AddSingleton<KrogerLocationsClient>();
        builder.Services.AddSingleton<KrogerCartService>();
        builder.Services.AddSingleton<KrogerWebAuthService>();

        var app = builder.Build();
        var store = app.Services.GetRequiredService<KrogerStore>();
        await store.InitializeAsync();

        app.MapKrogerEndpoints();

        await app.RunAsync();
        return 0;
    }
}
