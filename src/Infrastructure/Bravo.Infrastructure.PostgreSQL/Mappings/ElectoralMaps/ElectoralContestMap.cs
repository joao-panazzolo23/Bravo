using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectoralContestMap : EntityMapper<ElectoralContest>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectoralContest> builder)
    {
        builder.ToTable("ElectoralContests");

        builder.Property(x => x.ElectionRoundId).IsRequired();
        builder.Property(x => x.OfficeCode).IsRequired();
        builder.Property(x => x.OfficeName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StateId);

        // A contest is identified by its round, office and state scope. Null states are
        // distinct so that several national contests can coexist.
        builder.HasIndex(x => new { x.ElectionRoundId, x.OfficeCode, x.StateId })
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
