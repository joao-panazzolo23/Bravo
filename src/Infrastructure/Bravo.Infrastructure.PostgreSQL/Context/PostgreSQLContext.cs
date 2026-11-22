using Bravo.Domain.Auth.Entities;
using Bravo.Domain.Electoral.Entities;
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
    public DbSet<ImportFailedRecord> ImportFailedRecords { get; set; }
    public DbSet<ImportDataset> ImportDatasets { get; set; }
    public DbSet<DatasetVersion> DatasetVersions { get; set; }
    public DbSet<Election> Elections { get; set; }
    public DbSet<ElectionRound> ElectionRounds { get; set; }
    public DbSet<ElectoralOffice> ElectoralOffices { get; set; }
    public DbSet<PoliticalParty> PoliticalParties { get; set; }
    public DbSet<Federation> Federations { get; set; }
    public DbSet<Coalition> Coalitions { get; set; }
    public DbSet<ElectoralZone> ElectoralZones { get; set; }
    public DbSet<ElectoralSection> ElectoralSections { get; set; }
    public DbSet<PollingPlace> PollingPlaces { get; set; }
    public DbSet<PollingPlaceSection> PollingPlaceSections { get; set; }
    public DbSet<TseMunicipalityCode> TseMunicipalityCodes { get; set; }
    public DbSet<CandidateIdentity> CandidateIdentities { get; set; }
    public DbSet<Candidate> Candidates { get; set; }
    public DbSet<ElectionResult> ElectionResults { get; set; }
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
