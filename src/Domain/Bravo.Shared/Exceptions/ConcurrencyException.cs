namespace Bravo.Shared.Exceptions;

public class ConcurrencyException(string error) : DomainException(error)
{
    public override string Type { get; } = nameof(ConcurrencyException);
}
