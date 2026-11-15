using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class PollingPlaceMap : EntityMapper<PollingPlace>
{
    protected override void ConfigureMap(EntityTypeBuilder<PollingPlace> builder)
    {
        builder.ToTable("PollingPlaces");

        builder.Property(x => x.MunicipalityId).IsRequired();
        builder.Property(x => x.ZoneNumber).HasMaxLength(10).IsRequired();
        builder.Property(x => x.TseCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(250);
        builder.Property(x => x.Address).HasMaxLength(500);

        builder.HasIndex(x => new { x.MunicipalityId, x.ZoneNumber, x.TseCode }).IsUnique();

        builder.HasOne<Bravo.Domain.Geography.Entities.Municipality>()
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
