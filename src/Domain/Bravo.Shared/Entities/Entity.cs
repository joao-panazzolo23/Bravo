namespace Bravo.Shared.Entities;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Entity SetCreatedAt(DateTime utcNow)
    {
        CreatedAt = utcNow;
        return this;
    }

    public Entity SetUpdatedAt(DateTime utcNow)
    {
        UpdatedAt = utcNow;
        return this;
    }
}
