using Bravo.Domain.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ImportingMaps;

internal sealed class ImportDatasetMap : EntityMapper<ImportDataset>
{
    protected override void ConfigureMap(EntityTypeBuilder<ImportDataset> builder)
    {
        builder.ToTable("ImportDatasets");

        builder.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Dataset).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LayoutVersion).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SourceFileName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Checksum).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SizeBytes).IsRequired();
        builder.Property(x => x.AcquiredAt).IsRequired();

        builder.HasIndex(x => new { x.Provider, x.Dataset, x.ElectionYear, x.Checksum })
            .IsUnique();
    }
}
