using Bravo.Domain.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ImportingMaps;

internal sealed class ImportJobMap : EntityMapper<ImportJob>
{
    protected override void ConfigureMap(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJobs");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.BatchSize)
            .IsRequired();

        builder.Property(x => x.MaxAttempts)
            .IsRequired();

        builder.Property(x => x.LeaseDurationSeconds)
            .IsRequired();

        builder.Property(x => x.TotalProcessedRecords)
            .IsRequired();

        builder.Property(x => x.TotalFailedRecords)
            .IsRequired();

        builder.Property(x => x.LastError);

        builder.Property(x => x.StartedAt);
        builder.Property(x => x.CompletedAt);

        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
