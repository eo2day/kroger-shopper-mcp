namespace KrogerShopperMcp.Infrastructure;

internal static class EnvFileLoader
{
    private static bool _loaded;
    private const string AllowInsecureEnvVar = "KROGER_ALLOW_INSECURE_ENV_FILE";

    public static void TryLoad()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        var envPath = FindEnvFile();
        if (envPath is null || !File.Exists(envPath))
        {
            return;
        }

        if (!IsSecureEnough(envPath) && !IsInsecureEnvFileAllowed())
        {
            throw new InvalidOperationException(
                $"refusing to load env file with insecure permissions: {envPath}. " +
                $"Set {AllowInsecureEnvVar}=true to override.");
        }

        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindEnvFile()
    {
        var configuredPath = Environment.GetEnvironmentVariable("KROGER_ENV_FILE");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            return File.Exists(fullPath) ? fullPath : null;
        }

        var searchRoots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
        };

        var candidateNames = new[]
        {
            ".env",
            ".env.local",
            "kroger.env"
        };

        foreach (var root in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var candidateName in candidateNames)
            {
                var fullPath = Path.GetFullPath(Path.Combine(root, candidateName));
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    private static bool IsInsecureEnvFileAllowed()
    {
        return Environment.GetEnvironmentVariable(AllowInsecureEnvVar) switch
        {
            "1" or "true" or "TRUE" or "True" => true,
            _ => false
        };
    }

    private static bool IsSecureEnough(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            var mode = File.GetUnixFileMode(path);
            var insecureBits =
                UnixFileMode.GroupRead |
                UnixFileMode.GroupWrite |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherWrite |
                UnixFileMode.OtherExecute;

            return (mode & insecureBits) == 0;
        }
        catch
        {
            return true;
        }
    }
}
