namespace Bravo.Domain.Auth.ValueObjects;

public sealed record RecoveryToken(string Value, string Hash, DateTime ExpiresAt);
