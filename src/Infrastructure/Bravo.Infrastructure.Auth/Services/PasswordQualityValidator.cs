using Bravo.Domain.Auth.Services;
using Bravo.Domain.Auth.ValueObjects;
using Bravo.Infrastructure.Auth.Options;
using Microsoft.Extensions.Options;

namespace Bravo.Infrastructure.Auth.Services;

internal sealed class PasswordQualityValidator : IPasswordQualityValidator
{
    private readonly IReadOnlyList<PasswordRule> _rules;

    public PasswordQualityValidator(IOptions<PasswordPolicyOptions> options)
    {
        _rules = BuildRules(options.Value);
    }

    public PasswordValidationResult Validate(string password)
    {
        password ??= string.Empty;

        var errors = _rules.Where(rule => !rule.IsSatisfiedBy(password))
                           .Select(rule => rule.ErrorMessage)
                           .ToList();

        return errors.Count == 0
            ? PasswordValidationResult.Success
            : PasswordValidationResult.Failure(errors);
    }

    private static IReadOnlyList<PasswordRule> BuildRules(PasswordPolicyOptions policy)
    {
        List<PasswordRule> rules =
        [
            new("MinLength",
                $"A senha deve ter no mínimo {policy.MinLength} caracteres.",
                password => password.Length >= policy.MinLength),

            new("MaxLength",
                $"A senha deve ter no máximo {policy.MaxLength} caracteres.",
                password => password.Length <= policy.MaxLength)
        ];

        if (policy.RequireUppercase)
            rules.Add(new("Uppercase", "A senha deve conter ao menos uma letra maiúscula.", password => password.Any(char.IsUpper)));

        if (policy.RequireLowercase)
            rules.Add(new("Lowercase", "A senha deve conter ao menos uma letra minúscula.", password => password.Any(char.IsLower)));

        if (policy.RequireDigit)
            rules.Add(new("Digit", "A senha deve conter ao menos um número.", password => password.Any(char.IsDigit)));

        if (policy.RequireSpecialCharacter)
            rules.Add(new("SpecialCharacter", "A senha deve conter ao menos um caractere especial.", password => password.Any(c => !char.IsLetterOrDigit(c))));

        rules.Add(new("Entropy",
                      "A senha é muito fraca ou previsível.",
                      password => password.Length > policy.MaxLength
                                  || Zxcvbn.Core.EvaluatePassword(password).Score >= policy.MinEntropyScore));

        return rules;
    }
}

internal sealed record PasswordRule(string Code, string ErrorMessage, Func<string, bool> IsSatisfiedBy);
