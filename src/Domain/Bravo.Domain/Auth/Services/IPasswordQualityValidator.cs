using Bravo.Domain.Auth.ValueObjects;

namespace Bravo.Domain.Auth.Services;

public interface IPasswordQualityValidator
{
    PasswordValidationResult Validate(string password);
}
