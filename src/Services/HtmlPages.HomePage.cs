using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Models;

namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
    public static string RenderHomePage(KrogerConfig config, TokenSummary? status, string username)
    {
        var state = status is null
            ? "No token stored yet."
            : $"Token stored. Expires {status.ExpiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC.";
        var authLink = $"{config.PublicBaseUrl}/authorize";

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Kroger Assistant</title>
          {{FaviconLinks}}
          {{GoogleFonts}}
          <style>
            {{SharedAppStyles}}
            .shell { width: min(960px, calc(100% - 32px)); }
            .home-grid {
              display: grid;
              gap: 18px;
              grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            }
            .panel {
              padding: 18px;
              border: 1px solid var(--line);
              border-radius: 10px;
              background: rgba(255,255,255,.55);
            }
            .stack {
              display: grid;
              gap: 10px;
            }
            .inline-actions {
              display: flex;
              gap: 10px;
              flex-wrap: wrap;
            }
          </style>
        </head>
        <body>
          <div class="shell">
            <div class="topbar">
              <div>
                <p class="eyebrow">Kroger Assistant</p>
                <h1>Home</h1>
                <p class="meta">Signed in as <strong>{{username}}</strong></p>
              </div>
              <div class="actions">
                <a class="ghost-link" href="/saved-carts">Saved Carts</a>
                <a class="ghost-link" href="/current-cart">Current Cart</a>
              </div>
            </div>
            <div class="content">
              <div class="home-grid">
                <section class="panel stack">
                  <h2>Account Status</h2>
                  <p>{{state}}</p>
                  <p class="meta">Public callback host: <code>{{config.PublicBaseUrl}}/callback</code></p>
                </section>
                <section class="panel stack">
                  <h2>Quick Links</h2>
                  <div class="inline-actions">
                    <a class="button" href="/saved-carts">Browse Saved Carts</a>
                    <a class="button" href="/current-cart">View Current Cart</a>
                  </div>
                  <div class="inline-actions">
                    <a class="ghost-link" href="/change-password">Change Password</a>
                    <a class="ghost-link" href="/logout">Log Out</a>
                  </div>
                </section>
              </div>
              <section class="panel stack">
                <h2>Authorize Kroger</h2>
                <p class="meta">Use this only when you need to refresh or reconnect the Kroger account.</p>
                <div>
                  <a class="button" href="{{authLink}}">Authorize Kroger Account</a>
                </div>
              </section>
            </div>
          </div>
        </body>
        </html>
        """;
    }
}
