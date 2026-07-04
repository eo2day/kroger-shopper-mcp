using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Models;

namespace KrogerShopperMcp.Services;

internal static class HtmlPages
{
    public static string RenderHomePage(KrogerConfig config, TokenSummary? status)
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
          <style>
            body { font-family: Georgia, serif; margin: 0; min-height: 100vh; display: grid; place-items: center; background: linear-gradient(180deg,#f9f5ec,#efe5d3); color: #1f2a1f; }
            main { width: min(760px, calc(100% - 32px)); background: rgba(255,252,245,.92); border: 1px solid rgba(31,42,31,.12); border-radius: 24px; padding: 28px; box-shadow: 0 20px 60px rgba(49,42,23,.14); }
            a.button { display: inline-block; padding: 12px 16px; border-radius: 999px; background: #1f2a1f; color: #fff; text-decoration: none; font-weight: 700; }
            code, pre { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
            .muted { color: #5f6759; }
          </style>
        </head>
        <body>
          <main>
            <p class="muted">Persistent Kroger Assistant</p>
            <h1>Kroger OAuth Service</h1>
            <p>{{state}}</p>
            <p>Public callback host: <code>{{config.PublicBaseUrl}}/callback</code></p>
            <p><a class="button" href="{{authLink}}">Authorize Kroger Account</a></p>
          </main>
        </body>
        </html>
        """;
    }

    public static string RenderCallbackPage(bool ok, string title, string detail, string? state)
    {
        var color = ok ? "#2f6b3b" : "#b34a1b";
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Kroger OAuth Callback</title>
          <style>
            body { font-family: Georgia, serif; margin: 0; min-height: 100vh; display: grid; place-items: center; background: linear-gradient(180deg,#f9f5ec,#efe5d3); color: #1f2a1f; }
            main { width: min(760px, calc(100% - 32px)); background: rgba(255,252,245,.92); border: 1px solid rgba(31,42,31,.12); border-radius: 24px; padding: 28px; box-shadow: 0 20px 60px rgba(49,42,23,.14); }
            .pill { display:inline-block; padding:8px 10px; border-radius:999px; border:1px solid rgba(31,42,31,.12); color: {{color}}; font:700 .78rem/1 ui-monospace, SFMono-Regular, Menlo, monospace; text-transform:uppercase; letter-spacing:.08em; }
            code { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
          </style>
        </head>
        <body>
          <main>
            <div class="pill">{{(ok ? "Success" : "Failed")}}</div>
            <h1>{{title}}</h1>
            <p>{{detail}}</p>
            <p>State: <code>{{state ?? "(none)"}}</code></p>
          </main>
        </body>
        </html>
        """;
    }
}
