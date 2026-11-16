using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class PollingPlaceMap : EntityMapper<PollingPlace>
{
    protected override void ConfigureMap(EntityTypeBuilder<PollingPlace> builder)
    {
        builder.ToTable("PollingPlaces");

        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Neighborhood).HasMaxLength(150);
        builder.Property(x => x.ZipCode).HasMaxLength(10);
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);

        builder.HasIndex(x => new { x.StateId, x.ZoneId, x.TsePollingPlaceNumber }).IsUnique();

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Bravo.Domain.Geography.Entities.Municipality>()
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ElectoralZone>()
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
