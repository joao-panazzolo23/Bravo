using Bravo.Domain.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ImportingMaps;

internal sealed class ImportWorkUnitMap : EntityMapper<ImportWorkUnit>
{
    protected override void ConfigureMap(EntityTypeBuilder<ImportWorkUnit> builder)
    {
        builder.ToTable("ImportWorkUnits");

        builder.Property(x => x.UnitKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Cursor);
        builder.Property(x => x.LastError);

        builder.Property(x => x.LeaseOwner)
            .HasMaxLength(200);

        builder.Property(x => x.LeaseExpiresAt);
        builder.Property(x => x.NextAttemptAt);
        builder.Property(x => x.StartedAt);
        builder.Property(x => x.CompletedAt);

        builder.Property(x => x.ProcessedRecords).IsRequired();
        builder.Property(x => x.FailedRecords).IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.FencingToken).IsRequired();

        builder.Property(x => x.Stage)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.InsertedRecords).IsRequired();
        builder.Property(x => x.UpdatedRecords).IsRequired();
        builder.Property(x => x.RejectedRecords).IsRequired();
        builder.Property(x => x.TotalRecords);
        builder.Property(x => x.ProgressUpdatedAt);

        builder.HasIndex(x => new { x.JobId, x.UnitKey })
            .IsUnique();

        builder.HasIndex(x => new { x.JobId, x.Status, x.NextAttemptAt, x.CreatedAt })
            .HasFilter("\"Status\" IN ('Pending', 'Running')");

        builder.HasIndex(x => x.LeaseExpiresAt)
            .HasFilter("\"Status\" = 'Running'");

        builder.HasOne<ImportJob>()
            .WithMany()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
