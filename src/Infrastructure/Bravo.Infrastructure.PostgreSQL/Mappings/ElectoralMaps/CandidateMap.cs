using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class CandidateMap : EntityMapper<Candidate>
{
    protected override void ConfigureMap(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("Candidates");

        builder.Property(x => x.ElectionId).IsRequired();
        builder.Property(x => x.TseSequential).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Number).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.BallotName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.OfficeCode).IsRequired();
        builder.Property(x => x.OfficeName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PoliticalPartyId);
        builder.Property(x => x.StateId);
        builder.Property(x => x.SituationCode);
        builder.Property(x => x.SituationDescription).HasMaxLength(150);
        builder.Property(x => x.BirthDate);

        // The TSE sequential code is stable per election and is the natural key.
        builder.HasIndex(x => new { x.ElectionId, x.TseSequential }).IsUnique();
        builder.HasIndex(x => new { x.ElectionId, x.OfficeCode });
        builder.HasIndex(x => new { x.PoliticalPartyId, x.ElectionId });

        builder.HasOne(x => x.Election)
            .WithMany()
            .HasForeignKey(x => x.ElectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
