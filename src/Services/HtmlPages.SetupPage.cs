namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
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
}
