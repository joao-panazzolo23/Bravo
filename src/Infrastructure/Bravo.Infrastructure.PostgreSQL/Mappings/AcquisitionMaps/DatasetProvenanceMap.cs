using Bravo.Domain.Acquisition.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.AcquisitionMaps;

internal sealed class DatasetProvenanceMap : EntityMapper<DatasetProvenance>
{
    protected override void ConfigureMap(EntityTypeBuilder<DatasetProvenance> builder)
    {
        builder.ToTable("DatasetProvenances");

        builder.Property(x => x.ExecutionId)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DatasetIdentifier)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.SourceUrl)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.OriginalFilename)
            .HasMaxLength(250);

        builder.Property(x => x.DataDate);

        builder.Property(x => x.ContentHash)
            .HasMaxLength(128);

        builder.Property(x => x.FileSizeBytes);

        builder.Property(x => x.ETag)
            .HasMaxLength(200);

        builder.Property(x => x.SourceLastModifiedAt);

        builder.Property(x => x.FormatDescription)
            .HasMaxLength(50);

        builder.Property(x => x.Version)
            .HasMaxLength(50);

        builder.Property(x => x.FetchedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.ExecutionId, x.DatasetIdentifier });
        builder.HasIndex(x => new { x.Provider, x.DatasetIdentifier, x.FetchedAt });
    }
}
