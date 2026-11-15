using Bravo.Domain.Acquisition.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.AcquisitionMaps;

internal sealed class ImportCheckpointMap : EntityMapper<ImportCheckpoint>
{
    protected override void ConfigureMap(EntityTypeBuilder<ImportCheckpoint> builder)
    {
        builder.ToTable("ImportCheckpoints");

        builder.Property(x => x.ExecutionId)
            .IsRequired();

        builder.Property(x => x.DatasetKey)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.CheckpointData)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.RecordsCompleted)
            .IsRequired();

        builder.Property(x => x.CheckpointedAt)
            .IsRequired();

        // One checkpoint per execution and dataset key: recovery always reads the latest
        // committed state instead of accumulating duplicate rows per retry.
        builder.HasIndex(x => new { x.ExecutionId, x.DatasetKey })
            .IsUnique();
    }
}
