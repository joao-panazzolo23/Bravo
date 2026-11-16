using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectionStatisticsMap : EntityMapper<ElectionStatistics>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectionStatistics> builder)
    {
        builder.ToTable("ElectionStatistics");

        builder.HasIndex(x => new { x.ElectionRoundId, x.SectionId, x.DatasetVersionId }).IsUnique();
        builder.HasIndex(x => x.DatasetVersionId);

        builder.HasOne<ElectionRound>()
            .WithMany()
            .HasForeignKey(x => x.ElectionRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ElectoralSection>()
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Importing.Entities.DatasetVersion>()
            .WithMany()
            .HasForeignKey(x => x.DatasetVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
