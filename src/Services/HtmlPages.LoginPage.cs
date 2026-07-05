namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
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
}
