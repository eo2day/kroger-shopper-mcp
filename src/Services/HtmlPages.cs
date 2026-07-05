namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
    private const string GoogleFonts = """
        <link rel="preconnect" href="https://fonts.googleapis.com">
        <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
        <link href="https://fonts.googleapis.com/css2?family=Questrial&family=Space+Grotesk:wght@400;500;700&display=swap" rel="stylesheet">
        """;

    private const string FaviconLinks = """
        <link rel="icon" href="/favicon.svg" type="image/svg+xml">
        <link rel="shortcut icon" href="/favicon.svg" type="image/svg+xml">
        """;

    private const string BodyBg = """radial-gradient(circle at 20% 30%, rgba(86,108,232,0.4) 0%, transparent 50%), radial-gradient(circle at 80% 70%, rgba(42,62,184,0.3) 0%, transparent 40%), radial-gradient(circle at 50% 50%, rgba(74,98,224,0.2) 0%, transparent 60%), radial-gradient(circle at 10% 80%, rgba(86,108,232,0.25) 0%, transparent 45%), radial-gradient(circle at 90% 20%, rgba(42,62,184,0.35) 0%, transparent 35%), #3950d4""";

    private static readonly string SharedAppStyles = $$"""
            :root {
              color-scheme: light;
              --paper: rgba(255,252,245,.95);
              --panel: rgba(255,255,255,.76);
              --line: rgba(31,42,31,.12);
              --ink: #1f2a1f;
              --muted: #5f6759;
              --accent: #1c2a8f;
              --accent-soft: rgba(57,80,212,.12);
              --danger: #8f1c34;
            }

            * { box-sizing: border-box; }
            body {
              font-family: "Space Grotesk", "Avenir Next", "Segoe UI", "Trebuchet MS", sans-serif;
              margin: 0;
              min-height: 100vh;
              background: {{BodyBg}};
              color: var(--ink);
            }

            .shell {
              width: min(1360px, calc(100% - 32px));
              margin: 16px auto;
              background: var(--paper);
              border: 1px solid var(--line);
              border-radius: 10px;
              box-shadow: 0 20px 60px rgba(49,42,23,.14);
              overflow: hidden;
              backdrop-filter: blur(16px);
            }

            .topbar {
              display: flex;
              justify-content: space-between;
              align-items: center;
              gap: 16px;
              padding: 22px 24px;
              border-bottom: 1px solid var(--line);
              background: rgba(255,255,255,.5);
            }

            .eyebrow, .meta, .empty-copy, .price-note, .stock-note {
              color: var(--muted);
            }

            .eyebrow {
              margin: 0 0 8px;
              font-size: .82rem;
              text-transform: uppercase;
              letter-spacing: .08em;
              font-weight: 700;
            }

            h1, h2, h3 {
              font-family: "Century Gothic", "Questrial", "Avant Garde", "Trebuchet MS", sans-serif;
              font-weight: 700;
              text-transform: uppercase;
              margin: 0;
            }

            h1 { font-size: clamp(2rem, 4vw, 3.4rem); letter-spacing: 0; }
            h2 { font-size: 1.5rem; }
            h3 { font-size: 1rem; }

            .actions {
              display: flex;
              gap: 10px;
              flex-wrap: wrap;
              justify-content: flex-end;
            }

            .button, .ghost-link, .danger-button {
              display: inline-flex;
              align-items: center;
              justify-content: center;
              padding: 12px 16px;
              border-radius: 10px;
              text-decoration: none;
              font-weight: 700;
              border: 0;
              cursor: pointer;
              font: inherit;
            }

            .button { background: #1f2a1f; color: #fff; }
            .ghost-link { color: var(--accent); background: var(--accent-soft); }
            .danger-button { color: #fff; background: var(--danger); }

            .content {
              padding: 24px;
              display: grid;
              gap: 18px;
            }

            code, pre { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
            .muted { color: var(--muted); }
        """;

    private static readonly string CommonStyles = $$"""
            :root { color-scheme: light; --paper: rgba(255,252,245,.92); --line: rgba(31,42,31,.12); --muted: #5f6759; }
            body { font-family: "Space Grotesk", "Avenir Next", "Segoe UI", "Trebuchet MS", sans-serif; margin: 0; min-height: 100vh; display: grid; place-items: center; background: {{BodyBg}}; color: #1f2a1f; }
            main { width: min(760px, calc(100% - 32px)); background: rgba(255,252,245,.92); border: 1px solid rgba(31,42,31,.12); border-radius: 10px; padding: 28px; box-shadow: 0 20px 60px rgba(49,42,23,.14); }
            h1 { font-family: "Century Gothic", "Questrial", "Avant Garde", "Trebuchet MS", sans-serif; font-weight: 700; text-transform: uppercase; letter-spacing: -0.02em; }
            a.button { display: inline-block; padding: 12px 16px; border-radius: 10px; background: #1f2a1f; color: #fff; text-decoration: none; font-weight: 700; }
            code, pre { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
            .muted { color: #5f6759; }
        """;

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
          {{FaviconLinks}}
          {{GoogleFonts}}
          <style>
            {{CommonStyles}}
            form { display: grid; gap: 14px; }
            label { display: grid; gap: 6px; font-weight: 700; }
            input { font: inherit; padding: 12px 14px; border-radius: 10px; border: 1px solid rgba(31,42,31,.2); }
            button { display: inline-block; padding: 12px 16px; border-radius: 10px; background: #1f2a1f; color: #fff; border: 0; font: inherit; font-weight: 700; cursor: pointer; }
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
