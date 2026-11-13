namespace Bravo.Domain.Auth.ValueObjects;

public sealed record PasswordValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PasswordValidationResult Success { get; } = new(true, []);

    public static PasswordValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}
