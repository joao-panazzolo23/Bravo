using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class CoalitionMap : EntityMapper<Coalition>
{
    protected override void ConfigureMap(EntityTypeBuilder<Coalition> builder)
    {
        builder.ToTable("Coalitions");

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Composition);

        builder.HasIndex(x => new { x.ElectionRoundId, x.OfficeId, x.StateId, x.TseCoalitionNumber })
            .IsUnique();

        builder.HasOne<ElectionRound>()
            .WithMany()
            .HasForeignKey(x => x.ElectionRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ElectoralOffice>()
            .WithMany()
            .HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
