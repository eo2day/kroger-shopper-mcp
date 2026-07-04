namespace KrogerShopperMcp.Utilities;

internal static class CommandLineOptions
{
    public static string? GetOptionValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static string GetRequiredOptionValue(string[] args, string name)
    {
        return GetOptionValue(args, name)
               ?? throw new InvalidOperationException($"missing required option {name}");
    }
}
