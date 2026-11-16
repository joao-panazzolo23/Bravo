using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class PollingPlaceSectionMap : EntityMapper<PollingPlaceSection>
{
    protected override void ConfigureMap(EntityTypeBuilder<PollingPlaceSection> builder)
    {
        builder.ToTable("PollingPlaceSections");

        builder.HasIndex(x => new { x.ElectionRoundId, x.SectionId }).IsUnique();
        builder.HasIndex(x => x.PollingPlaceId);

        builder.HasOne<ElectionRound>()
            .WithMany()
            .HasForeignKey(x => x.ElectionRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PollingPlace>()
            .WithMany()
            .HasForeignKey(x => x.PollingPlaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ElectoralSection>()
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
