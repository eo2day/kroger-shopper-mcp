namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
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
          {{FaviconLinks}}
          {{GoogleFonts}}
          <style>
            {{CommonStyles}}
            form { display: grid; gap: 14px; }
            label { display: grid; gap: 6px; font-weight: 700; }
            input { font: inherit; padding: 12px 14px; border-radius: 10px; border: 1px solid rgba(31,42,31,.2); }
            button { display: inline-block; padding: 12px 16px; border-radius: 10px; background: #1f2a1f; color: #fff; border: 0; font: inherit; font-weight: 700; cursor: pointer; }
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
}
