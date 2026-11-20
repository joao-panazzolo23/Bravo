using Bravo.Domain.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ImportingMaps;

internal sealed class ImportFailedRecordMap : EntityMapper<ImportFailedRecord>
{
    protected override void ConfigureMap(EntityTypeBuilder<ImportFailedRecord> builder)
    {
        builder.ToTable("ImportFailedRecords");

        builder.Property(x => x.SourceIdentifier)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Payload);

        builder.Property(x => x.ErrorCategory)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ErrorDescription)
            .IsRequired();

        builder.Property(x => x.AttemptCount)
            .IsRequired();

        builder.HasIndex(x => new { x.JobId, x.SourceIdentifier })
            .IsUnique();

        builder.HasIndex(x => new { x.JobId, x.CreatedAt });

        builder.HasOne<ImportJob>()
            .WithMany()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
