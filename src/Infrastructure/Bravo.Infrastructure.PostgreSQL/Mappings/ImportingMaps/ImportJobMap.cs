using Bravo.Domain.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ImportingMaps;

internal sealed class ImportJobMap : EntityMapper<ImportJob>
{
    protected override void ConfigureMap(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJobs");

        builder.Property(x => x.Source)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Scope)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Stage)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.BatchSize).IsRequired();
        builder.Property(x => x.MaxAttempts).IsRequired();
        builder.Property(x => x.MaxParallelism).IsRequired();
        builder.Property(x => x.LeaseDurationSeconds).IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();

        builder.Property(x => x.TotalOperations).IsRequired();
        builder.Property(x => x.CompletedOperations).IsRequired();
        builder.Property(x => x.FailedOperations).IsRequired();
        builder.Property(x => x.TotalProcessedRecords).IsRequired();
        builder.Property(x => x.TotalFailedRecords).IsRequired();

        builder.Property(x => x.CurrentOperation).HasMaxLength(200);
        builder.Property(x => x.LastError);

        builder.Property(x => x.LeaseOwner).HasMaxLength(200);
        builder.Property(x => x.LeaseExpiresAt);
        builder.Property(x => x.NextAttemptAt);
        builder.Property(x => x.ProgressUpdatedAt);
        builder.Property(x => x.StartedAt);
        builder.Property(x => x.CompletedAt);

        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.NextAttemptAt)
            .HasFilter("\"Status\" = 'Pending'");
        builder.HasIndex(x => x.LeaseExpiresAt)
            .HasFilter("\"Status\" = 'Running'");

        // Two active jobs for the same source and scope must never coexist. The partial
        // index is the durable backstop behind the advisory-lock overlap check.
        builder.HasIndex(x => new { x.Source, x.Scope })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Pending', 'Running')");
    }
}
