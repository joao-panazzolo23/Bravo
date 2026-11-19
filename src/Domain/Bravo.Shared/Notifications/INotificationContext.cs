namespace Bravo.Shared.Notifications;

public interface INotificationContext
{
    bool HasNotifications { get; }

    IReadOnlyCollection<INotification> Notifications { get; }

    void Add(INotification notification);
}
