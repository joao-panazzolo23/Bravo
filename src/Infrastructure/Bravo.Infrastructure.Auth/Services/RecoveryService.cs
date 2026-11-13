using Bravo.Domain.Auth.Services;
using Bravo.Domain.Auth.ValueObjects;
using Bravo.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Bravo.Infrastructure.Auth.Services;

internal sealed class RecoveryService(
    IOptions<RecoveryOptions> options,
    TimeProvider timeProvider
    ) : IRecoveryService
{
    private const int TokenSize = 32;
    private readonly RecoveryOptions _options = options.Value;

    public RecoveryToken Generate()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSize));
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.Add(_options.TokenLifetime);

        return new RecoveryToken(token, Hash(token), expiresAt);
    }

    public string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
