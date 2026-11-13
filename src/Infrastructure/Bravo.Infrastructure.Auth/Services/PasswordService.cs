using Bravo.Domain.Auth.Enums;
using Bravo.Domain.Auth.Services;
using System.Security.Cryptography;
using System.Text;

namespace Bravo.Infrastructure.Auth.Services;

/// <summary>
/// PBKDF2-HMAC-SHA256, stored as "v2$iterations$salt$hash".
///
/// Hashes created by the old implementation (base64 of salt[16] + HMAC-SHA256) still verify,
/// but ask for a rehash, so users are migrated on their next login.
/// </summary>
internal sealed class PasswordService : IPasswordService
{
    private const string Version = "v2";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 600_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return string.Join('$', Version, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerification Verify(string passwordHash, string password)
    {
        if (passwordHash.Split('$') is [Version, var iterations, var salt, var hash]
            && int.TryParse(iterations, out var iterationCount)
            && iterationCount > 0)
        {
            return VerifyPbkdf2(password, iterationCount, salt, hash);
        }

        return VerifyLegacy(password, passwordHash);
    }

    private static PasswordVerification VerifyPbkdf2(string password, int iterations, string salt, string hash)
    {
        byte[] saltBytes, expected;

        try
        {
            saltBytes = Convert.FromBase64String(salt);
            expected = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return PasswordVerification.Failed;
        }

        if (saltBytes.Length == 0 || expected.Length == 0)
            return PasswordVerification.Failed;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterations, Algorithm, expected.Length);

        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return PasswordVerification.Failed;

        return iterations < Iterations
            ? PasswordVerification.SuccessRehashNeeded
            : PasswordVerification.Success;
    }

    private static PasswordVerification VerifyLegacy(string password, string passwordHash)
    {
        Span<byte> stored = stackalloc byte[64];

        if (!Convert.TryFromBase64String(passwordHash, stored, out var written) || written <= SaltSize)
            return PasswordVerification.Failed;

        var actual = HMACSHA256.HashData(stored[..SaltSize], Encoding.UTF8.GetBytes(password));

        return CryptographicOperations.FixedTimeEquals(actual, stored[SaltSize..written])
            ? PasswordVerification.SuccessRehashNeeded
            : PasswordVerification.Failed;
    }
}
