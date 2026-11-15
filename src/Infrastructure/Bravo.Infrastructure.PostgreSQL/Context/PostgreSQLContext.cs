using Bravo.Domain.Auth.Entities;
using Bravo.Domain.Geography.Entities;
using Bravo.Domain.Importing.Entities;
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
    public DbSet<ImportJob> ImportJobs { get; set; }
    public DbSet<ImportWorkUnit> ImportWorkUnits { get; set; }
    public DbSet<ImportFailedRecord> ImportFailedRecords { get; set; }
    public DbSet<State> States { get; set; }
    public DbSet<Municipality> Municipalities { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(PostgreSQLContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
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
