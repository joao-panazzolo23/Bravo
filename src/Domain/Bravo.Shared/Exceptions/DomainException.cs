namespace Bravo.Shared.Exceptions;

public class DomainException(string error) : Exception
{
    public string Error { get ; private set; } = error;
    public virtual string Type { get; } = nameof(DomainException);
}
