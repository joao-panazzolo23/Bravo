namespace Bravo.Domain.Auth.ValueObjects;

public sealed record RefreshTokenPayload(Guid UserId, Guid SecurityStamp);
