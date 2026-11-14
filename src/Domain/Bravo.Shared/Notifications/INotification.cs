namespace Bravo.Shared.Notifications;

public interface INotification
{
    string Code { get; }
    string Message { get; }
}

public sealed record DomainNotification(
    string Code,
    string Message
) : INotification;
