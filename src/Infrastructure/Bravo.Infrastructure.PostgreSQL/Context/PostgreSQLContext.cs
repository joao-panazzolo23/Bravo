using Bravo.Domain.Auth.Entities;
using Bravo.Domain.Users.Entities;
using Bravo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bravo.Infrastructure.PostgreSQL.Context;

public class PostgreSQLContext(DbContextOptions<PostgreSQLContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserRecovery> UserRecoveries { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(PostgreSQLContext).Assembly);
    }
    /// <summary>
    /// At this very moment, DomainEvents are NOT implemented. We're only declaring ApplicationEvents at Application level
    /// To set it inside entities, we would probably break clean arch/DDD principles using Mediator's interfaces
    /// 
    /// Or use mediator's object overload. Not sure if is a good idea, think about it.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreatedAt(DateTime.UtcNow);
                    break;

                case EntityState.Modified:
                    entry.Entity.SetUpdatedAt(DateTime.UtcNow);
                    break;
            }
        }
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
