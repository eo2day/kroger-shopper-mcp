using KrogerShopperMcp.Configuration;
using KrogerShopperMcp.Models;

namespace KrogerShopperMcp.Services;

internal static class HtmlPages
{
    private const string GoogleFonts = """
        <link rel="preconnect" href="https://fonts.googleapis.com">
        <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
        <link href="https://fonts.googleapis.com/css2?family=Questrial&family=Space+Grotesk:wght@400;500;700&display=swap" rel="stylesheet">
        """;

    private const string BodyBg = """radial-gradient(circle at 20% 30%, rgba(86,108,232,0.4) 0%, transparent 50%), radial-gradient(circle at 80% 70%, rgba(42,62,184,0.3) 0%, transparent 40%), radial-gradient(circle at 50% 50%, rgba(74,98,224,0.2) 0%, transparent 60%), radial-gradient(circle at 10% 80%, rgba(86,108,232,0.25) 0%, transparent 45%), radial-gradient(circle at 90% 20%, rgba(42,62,184,0.35) 0%, transparent 35%), #3950d4""";

    private const string CommonStyles = """
            body { font-family: "Space Grotesk", "Avenir Next", "Segoe UI", "Trebuchet MS", sans-serif; margin: 0; min-height: 100vh; display: grid; place-items: center; background: radial-gradient(circle at 20% 30%, rgba(86,108,232,0.4) 0%, transparent 50%), radial-gradient(circle at 80% 70%, rgba(42,62,184,0.3) 0%, transparent 40%), radial-gradient(circle at 50% 50%, rgba(74,98,224,0.2) 0%, transparent 60%), radial-gradient(circle at 10% 80%, rgba(86,108,232,0.25) 0%, transparent 45%), radial-gradient(circle at 90% 20%, rgba(42,62,184,0.35) 0%, transparent 35%), #3950d4; color: #1f2a1f; }
            main { width: min(760px, calc(100% - 32px)); background: rgba(255,252,245,.92); border: 1px solid rgba(31,42,31,.12); border-radius: 24px; padding: 28px; box-shadow: 0 20px 60px rgba(49,42,23,.14); }
            h1 { font-family: "Century Gothic", "Questrial", "Avant Garde", "Trebuchet MS", sans-serif; font-weight: 700; text-transform: uppercase; letter-spacing: -0.02em; }
            a.button { display: inline-block; padding: 12px 16px; border-radius: 999px; background: #1f2a1f; color: #fff; text-decoration: none; font-weight: 700; }
            code, pre { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
            .muted { color: #5f6759; }
        """;

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
          {{GoogleFonts}}
          <style>
            {{CommonStyles}}
          </style>
        </head>
        <body>
          <main>
            <p class="muted">Persistent Kroger Assistant</p>
            <h1>Kroger OAuth Service</h1>
            <p>{{state}}</p>
            <p>Public callback host: <code>{{config.PublicBaseUrl}}/callback</code></p>
            <p class="muted">Signed in as <strong>{{username}}</strong></p>
            <p><a class="button" href="{{authLink}}">Authorize Kroger Account</a></p>
            <p><a href="/change-password">Change password</a></p>
            <p><a href="/logout">Log out</a></p>
          </main>
        </body>
        </html>
        """;
    }

    public static string RenderSetupPage(string? error = null)
    {
        return RenderCredentialPage(
            title: "Set Up Kroger Assistant Login",
            description: "Create the local username and password that will protect the authorize flow.",
            actionPath: "/setup",
            buttonText: "Create Login",
            showUsername: true,
            usernameValue: null,
            error: error);
    }

    public static string RenderLoginPage(string username, string? error = null)
    {
        return RenderCredentialPage(
            title: "Sign In",
            description: "Sign in to access the Kroger authorize flow.",
            actionPath: "/login",
            buttonText: "Sign In",
            showUsername: true,
            usernameValue: null,
            error: error);
    }

    public static string RenderChangePasswordPage(string username, string? error = null, string? success = null)
    {
        var errorBlock = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"<p class=\"error\">{System.Net.WebUtility.HtmlEncode(error)}</p>";
        var successBlock = string.IsNullOrWhiteSpace(success)
            ? string.Empty
            : $"<p class=\"success\">{System.Net.WebUtility.HtmlEncode(success)}</p>";

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Change Password</title>
          {{GoogleFonts}}
          <style>
            {{CommonStyles}}
            form { display: grid; gap: 14px; }
            label { display: grid; gap: 6px; font-weight: 700; }
            input { font: inherit; padding: 12px 14px; border-radius: 12px; border: 1px solid rgba(31,42,31,.2); }
            button { display: inline-block; padding: 12px 16px; border-radius: 999px; background: #1f2a1f; color: #fff; border: 0; font: inherit; font-weight: 700; cursor: pointer; }
            .error { color: #a33712; font-weight: 700; }
            .success { color: #2f6b3b; font-weight: 700; }
          </style>
        </head>
        <body>
          <main>
            <p class="muted">Kroger Assistant Access</p>
            <h1>Change Password</h1>
            <p>Signed in as <strong>{{username}}</strong>.</p>
            {{errorBlock}}
            {{successBlock}}
            <form method="post" action="/change-password">
              <label>
                <span>Current password</span>
                <input type="password" name="current_password" autocomplete="current-password" required>
              </label>
              <label>
                <span>New password</span>
                <input type="password" name="new_password" autocomplete="new-password" required>
              </label>
              <button type="submit">Update Password</button>
            </form>
            <p><a href="/">Back</a></p>
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
          {{GoogleFonts}}
          <style>
            {{CommonStyles}}
            .pill { display:inline-block; padding:8px 10px; border-radius:999px; border:1px solid rgba(31,42,31,.12); color: {{color}}; font:700 .78rem/1 ui-monospace, SFMono-Regular, Menlo, monospace; text-transform:uppercase; letter-spacing:.08em; }
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

    private static string RenderCredentialPage(
        string title,
        string description,
        string actionPath,
        string buttonText,
        bool showUsername,
        string? usernameValue,
        string? error)
    {
        var usernameInput = showUsername
            ? $$"""
              <label>
                <span>Username</span>
                <input type="text" name="username" value="{{System.Net.WebUtility.HtmlEncode(usernameValue ?? string.Empty)}}" autocomplete="username" required>
              </label>
              """
            : $$"""<input type="hidden" name="username" value="{{System.Net.WebUtility.HtmlEncode(usernameValue ?? string.Empty)}}">""";
        var errorBlock = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"<p class=\"error\">{System.Net.WebUtility.HtmlEncode(error)}</p>";

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{title}}</title>
          {{GoogleFonts}}
          <style>
            {{CommonStyles}}
            form { display: grid; gap: 14px; }
            label { display: grid; gap: 6px; font-weight: 700; }
            input { font: inherit; padding: 12px 14px; border-radius: 12px; border: 1px solid rgba(31,42,31,.2); }
            button { display: inline-block; padding: 12px 16px; border-radius: 999px; background: #1f2a1f; color: #fff; border: 0; font: inherit; font-weight: 700; cursor: pointer; }
            .error { color: #a33712; font-weight: 700; }
          </style>
        </head>
        <body>
          <main>
            <p class="muted">Kroger Assistant Access</p>
            <h1>{{title}}</h1>
            <p>{{description}}</p>
            {{errorBlock}}
            <form method="post" action="{{actionPath}}">
              {{usernameInput}}
              <label>
                <span>Password</span>
                <input type="password" name="password" autocomplete="current-password" required>
              </label>
              <button type="submit">{{buttonText}}</button>
            </form>
          </main>
        </body>
        </html>
        """;
    }
}
