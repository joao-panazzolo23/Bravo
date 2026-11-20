using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectoralSectionMap : EntityMapper<ElectoralSection>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectoralSection> builder)
    {
        builder.ToTable("ElectoralSections");

        builder.HasIndex(x => new { x.MunicipalityId, x.ZoneId, x.TseSectionNumber }).IsUnique();
        builder.HasIndex(x => x.ZoneId);

        builder.HasOne<Domain.Geography.Entities.Municipality>()
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ElectoralZone>()
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
