namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
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
          {{FaviconLinks}}
          {{GoogleFonts}}
          <style>
            {{CommonStyles}}
            .pill { display:inline-block; padding:8px 10px; border-radius:10px; border:1px solid rgba(31,42,31,.12); color: {{color}}; font:700 .78rem/1 ui-monospace, SFMono-Regular, Menlo, monospace; text-transform:uppercase; letter-spacing:.08em; }
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
