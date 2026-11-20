using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectionResultMap : EntityMapper<ElectionResult>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectionResult> builder)
    {
        builder.ToTable("ElectionResults", table =>
        {
            table.HasCheckConstraint(
                "CK_ElectionResults_NominalRequiresCandidate",
                "\"VoteType\" <> 1 OR \"CandidateId\" IS NOT NULL");

            table.HasCheckConstraint(
                "CK_ElectionResults_PartyRequiresParty",
                "\"VoteType\" <> 2 OR \"PartyId\" IS NOT NULL");

            table.HasCheckConstraint(
                "CK_ElectionResults_BlankNullHaveNoReferences",
                "\"VoteType\" NOT IN (3, 4) OR (\"CandidateId\" IS NULL AND \"PartyId\" IS NULL)");
        });

        builder.Property(x => x.VoteType).IsRequired();
        builder.Property(x => x.TseBallotNumber).IsRequired();
        builder.Property(x => x.Votes).IsRequired();

        // PostgreSQL treats NULLs as distinct in a plain unique index, so without NULLS NOT
        // DISTINCT the upsert never conflicts for party votes (PartyId set, CandidateId null)
        // and blank votes (both null). Every re-execution then inserted a second copy of those
        // rows, doubling vote totals. NULLS NOT DISTINCT is the native way to make the index
        // match the identity the import actually upserts on.
        builder.HasIndex(x => new
        {
            x.ElectionRoundId,
            x.SectionId,
            x.OfficeId,
            x.VoteType,
            x.CandidateId,
            x.PartyId,
            x.TseBallotNumber,
            x.DatasetVersionId
        })
        .IsUnique()
        .AreNullsDistinct(false);

        builder.HasIndex(x => new { x.ElectionRoundId, x.SectionId, x.OfficeId });
        builder.HasIndex(x => x.DatasetVersionId);

        builder.HasOne<ElectionRound>()
            .WithMany()
            .HasForeignKey(x => x.ElectionRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ElectoralSection>()
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ElectoralOffice>()
            .WithMany()
            .HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PoliticalParty>()
            .WithMany()
            .HasForeignKey(x => x.PartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Importing.Entities.DatasetVersion>()
            .WithMany()
            .HasForeignKey(x => x.DatasetVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
