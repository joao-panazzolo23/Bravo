using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class PollingSectionMap : EntityMapper<PollingSection>
{
    protected override void ConfigureMap(EntityTypeBuilder<PollingSection> builder)
    {
        builder.ToTable("PollingSections");

        builder.Property(x => x.StateId).IsRequired();
        builder.Property(x => x.ZoneNumber).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Number).HasMaxLength(10).IsRequired();
        builder.Property(x => x.MunicipalityId).IsRequired();
        builder.Property(x => x.PollingPlaceId);

        // A section is identified by its state, zone and number; the municipality and the
        // polling place are attributes that may be corrected by a later dataset.
        builder.HasIndex(x => new { x.StateId, x.ZoneNumber, x.Number }).IsUnique();
        builder.HasIndex(x => x.MunicipalityId);

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Bravo.Domain.Geography.Entities.Municipality>()
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PollingPlace>()
            .WithMany()
            .HasForeignKey(x => x.PollingPlaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
