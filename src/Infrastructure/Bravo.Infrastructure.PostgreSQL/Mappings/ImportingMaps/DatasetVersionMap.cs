using Bravo.Domain.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ImportingMaps;

internal sealed class DatasetVersionMap : EntityMapper<DatasetVersion>
{
    protected override void ConfigureMap(EntityTypeBuilder<DatasetVersion> builder)
    {
        builder.ToTable("DatasetVersions");

        builder.Property(x => x.Scope).HasMaxLength(150).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.ActivatedAt);
        builder.Property(x => x.SupersededAt);

        builder.HasIndex(x => new { x.ImportDatasetId, x.Scope })
            .IsUnique();

        builder.HasIndex(x => x.Scope)
            .IsUnique()
            .HasFilter("\"IsActive\"");

        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        builder.HasOne<ImportDataset>()
            .WithMany()
            .HasForeignKey(x => x.ImportDatasetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ImportJob>()
            .WithMany()
            .HasForeignKey(x => x.ImportJobId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
