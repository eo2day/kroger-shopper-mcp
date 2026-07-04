using System.Security.Cryptography;
using System.Text;
using KrogerShopperMcp.Infrastructure;

namespace KrogerShopperMcp.Services;

internal sealed class KrogerWebAuthService
{
    public const string SessionCookieName = "kroger_assistant_session";
    private const int PasswordIterations = 120_000;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);

    public async Task<bool> HasCredentialAsync(KrogerStore store)
    {
        return await store.GetWebCredentialAsync() is not null;
    }

    public async Task<(bool Ok, string Error)> CreateInitialCredentialAsync(
        KrogerStore store,
        string username,
        string password)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return (false, "Username is required.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
        {
            return (false, "Password must be at least 10 characters.");
        }

        if (await store.GetWebCredentialAsync() is not null)
        {
            return (false, "Credentials are already configured.");
        }

        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = HashPassword(password, saltBytes, PasswordIterations);

        await store.UpsertWebCredentialAsync(
            normalizedUsername,
            Convert.ToBase64String(hashBytes),
            Convert.ToBase64String(saltBytes),
            PasswordIterations);

        return (true, string.Empty);
    }

    public async Task<bool> ValidateCredentialAsync(KrogerStore store, string username, string password)
    {
        var credential = await store.GetWebCredentialAsync();
        if (credential is null)
        {
            return false;
        }

        var normalizedUsername = string.IsNullOrWhiteSpace(username)
            ? credential.Username
            : NormalizeUsername(username);

        if (!string.Equals(NormalizeUsername(credential.Username), normalizedUsername, StringComparison.Ordinal))
        {
            return false;
        }

        var saltBytes = Convert.FromBase64String(credential.PasswordSalt);
        var expectedHash = Convert.FromBase64String(credential.PasswordHash);
        var providedHash = HashPassword(password, saltBytes, credential.PasswordIterations);
        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }

    public async Task<(bool Ok, string Error)> ChangePasswordAsync(
        KrogerStore store,
        string currentPassword,
        string newPassword)
    {
        var credential = await store.GetWebCredentialAsync();
        if (credential is null)
        {
            return (false, "Credentials are not configured.");
        }

        var currentIsValid = await ValidateCredentialAsync(store, credential.Username, currentPassword);
        if (!currentIsValid)
        {
            return (false, "Current password is incorrect.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 10)
        {
            return (false, "New password must be at least 10 characters.");
        }

        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = HashPassword(newPassword, saltBytes, PasswordIterations);
        await store.UpsertWebCredentialAsync(
            credential.Username,
            Convert.ToBase64String(hashBytes),
            Convert.ToBase64String(saltBytes),
            PasswordIterations);

        return (true, string.Empty);
    }

    public async Task<string> CreateSessionAsync(KrogerStore store, string username)
    {
        var sessionId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(SessionLifetime);
        await store.CreateWebSessionAsync(sessionId, NormalizeUsername(username), expiresAtUtc);
        return sessionId;
    }

    public async Task<bool> IsAuthenticatedAsync(KrogerStore store, string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var session = await store.GetWebSessionAsync(sessionId);
        if (session is null)
        {
            return false;
        }

        if (session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await store.DeleteWebSessionAsync(sessionId);
            return false;
        }

        return true;
    }

    public Task DeleteSessionAsync(KrogerStore store, string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Task.CompletedTask;
        }

        return store.DeleteWebSessionAsync(sessionId);
    }

    private static byte[] HashPassword(string password, byte[] saltBytes, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            iterations,
            HashAlgorithmName.SHA256,
            32);
    }

    private static string NormalizeUsername(string? username)
    {
        return username?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
