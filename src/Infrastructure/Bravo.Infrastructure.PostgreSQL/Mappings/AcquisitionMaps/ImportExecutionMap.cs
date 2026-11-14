using Bravo.Domain.Acquisition.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.AcquisitionMaps;

internal sealed class ImportExecutionMap : EntityMapper<ImportExecution>
{
    protected override void ConfigureMap(EntityTypeBuilder<ImportExecution> builder)
    {
        builder.ToTable("ImportExecutions");

        builder.Property(x => x.OperationType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Scope)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.StartedAt);
        builder.Property(x => x.CompletedAt);

        builder.Property(x => x.CurrentStage)
            .HasMaxLength(100);

        builder.Property(x => x.RecordsProcessed)
            .IsRequired();

        builder.Property(x => x.RecordsPersisted)
            .IsRequired();

        builder.Property(x => x.FailureDetails)
            .HasMaxLength(4000);

        builder.Property(x => x.CancellationReason)
            .HasMaxLength(500);

        builder.HasMany(x => x.Checkpoints)
            .WithOne(x => x.Execution)
            .HasForeignKey(x => x.ExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Provenances)
            .WithOne(x => x.Execution)
            .HasForeignKey(x => x.ExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.OperationType, x.Status });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

