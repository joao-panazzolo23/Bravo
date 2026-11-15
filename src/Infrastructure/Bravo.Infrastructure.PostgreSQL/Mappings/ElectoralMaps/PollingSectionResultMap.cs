using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class PollingSectionResultMap : EntityMapper<PollingSectionResult>
{
    protected override void ConfigureMap(EntityTypeBuilder<PollingSectionResult> builder)
    {
        builder.ToTable("PollingSectionResults");

        builder.Property(x => x.ElectionId).IsRequired();
        builder.Property(x => x.ElectionRoundId).IsRequired();
        builder.Property(x => x.ElectoralContestId).IsRequired();
        builder.Property(x => x.PollingSectionId).IsRequired();
        builder.Property(x => x.CandidateId).IsRequired();
        builder.Property(x => x.Votes).IsRequired();
        builder.Property(x => x.ProvenanceId);

        // Natural key of a vote fact: contest + section + candidate.
        builder.HasIndex(x => new { x.ElectoralContestId, x.PollingSectionId, x.CandidateId })
            .IsUnique()
            .HasDatabaseName("UX_PollingSectionResults_Contest_Section_Candidate");

        builder.HasIndex(x => new { x.ElectionId, x.ElectionRoundId });
        builder.HasIndex(x => new { x.ElectionId, x.CandidateId });
        builder.HasIndex(x => x.ProvenanceId);

        builder.HasOne<Bravo.Domain.Acquisition.Entities.DatasetProvenance>()
            .WithMany()
            .HasForeignKey(x => x.ProvenanceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
