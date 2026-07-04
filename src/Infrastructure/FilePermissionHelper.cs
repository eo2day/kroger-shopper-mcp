namespace KrogerShopperMcp.Infrastructure;

internal static class FilePermissionHelper
{
    public static void TryHardenOwnerOnly(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows() || !File.Exists(path))
            {
                return;
            }

            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);
        }
        catch
        {
            // Best-effort only. Do not block app startup on platform-specific permission issues.
        }
    }
}
