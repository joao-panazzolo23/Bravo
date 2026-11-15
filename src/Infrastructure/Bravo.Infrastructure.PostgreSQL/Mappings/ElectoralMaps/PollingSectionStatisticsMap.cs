using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class PollingSectionStatisticsMap : EntityMapper<PollingSectionStatistics>
{
    protected override void ConfigureMap(EntityTypeBuilder<PollingSectionStatistics> builder)
    {
        builder.ToTable("PollingSectionStatistics");

        builder.Property(x => x.ElectionId).IsRequired();
        builder.Property(x => x.ElectionRoundId).IsRequired();
        builder.Property(x => x.ElectoralContestId).IsRequired();
        builder.Property(x => x.PollingSectionId).IsRequired();
        builder.Property(x => x.EligibleVoters);
        builder.Property(x => x.Attendees);
        builder.Property(x => x.Absentees);
        builder.Property(x => x.NominalVotes);
        builder.Property(x => x.BlankVotes);
        builder.Property(x => x.NullVotes);
        builder.Property(x => x.PartyVotes);
        builder.Property(x => x.ProvenanceId);

        builder.HasIndex(x => new { x.ElectoralContestId, x.PollingSectionId })
            .IsUnique()
            .HasDatabaseName("UX_PollingSectionStatistics_Contest_Section");

        builder.HasIndex(x => new { x.ElectionId, x.ElectionRoundId });
        builder.HasIndex(x => x.ProvenanceId);

        builder.HasOne<Bravo.Domain.Acquisition.Entities.DatasetProvenance>()
            .WithMany()
            .HasForeignKey(x => x.ProvenanceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
