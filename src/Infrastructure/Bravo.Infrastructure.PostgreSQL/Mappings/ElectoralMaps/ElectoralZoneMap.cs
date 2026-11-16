using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectoralZoneMap : EntityMapper<ElectoralZone>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectoralZone> builder)
    {
        builder.ToTable("ElectoralZones");

        builder.HasIndex(x => new { x.StateId, x.TseZoneNumber }).IsUnique();
        builder.HasIndex(x => x.MunicipalityId);

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Bravo.Domain.Geography.Entities.Municipality>()
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
