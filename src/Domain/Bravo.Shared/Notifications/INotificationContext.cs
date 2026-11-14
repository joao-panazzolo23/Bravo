namespace Bravo.Shared.Notifications;

public interface INotificationContext
{
    bool HasNotifications { get; }

    IReadOnlyCollection<INotification> Notifications { get; }

    void Add(INotification notification);
}

public sealed class NotificationContext : INotificationContext
{
    private readonly List<INotification> _notifications = [];

    public bool HasNotifications => _notifications.Count > 0;

    public IReadOnlyCollection<INotification> Notifications
        => _notifications.AsReadOnly();

    public void Add(INotification notification)
        => _notifications.Add(notification);
}
