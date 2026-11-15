using Bravo.Domain.Acquisition.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.AcquisitionMaps;

internal sealed class ImportRejectionMap : EntityMapper<ImportRejection>
{
    protected override void ConfigureMap(EntityTypeBuilder<ImportRejection> builder)
    {
        builder.ToTable("ImportRejections");

        builder.Property(x => x.ExecutionId).IsRequired();
        builder.Property(x => x.DatasetKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RecordReference).HasMaxLength(500);
        builder.Property(x => x.PayloadSample).HasMaxLength(2000);

        builder.HasIndex(x => new { x.ExecutionId, x.DatasetKey });
    }
}
