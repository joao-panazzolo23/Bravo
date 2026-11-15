using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectoralZoneMap : EntityMapper<ElectoralZone>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectoralZone> builder)
    {
        builder.ToTable("ElectoralZones");

        builder.Property(x => x.StateId).IsRequired();
        builder.Property(x => x.Number).HasMaxLength(10).IsRequired();

        builder.HasIndex(x => new { x.StateId, x.Number }).IsUnique();

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
