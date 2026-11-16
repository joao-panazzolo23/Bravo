using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class CandidateMap : EntityMapper<Candidate>
{
    protected override void ConfigureMap(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("Candidates");

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UrnName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SocialName).HasMaxLength(200);
        builder.Property(x => x.RegistrationStatus).HasMaxLength(100);
        builder.Property(x => x.Gender).HasMaxLength(150);
        builder.Property(x => x.Education).HasMaxLength(150);
        builder.Property(x => x.Race).HasMaxLength(150);
        builder.Property(x => x.Occupation).HasMaxLength(150);
        builder.Property(x => x.BirthDate);

        builder.HasIndex(x => new { x.ElectionId, x.TseCandidateSequential }).IsUnique();
        builder.HasIndex(x => new { x.ElectionId, x.OfficeId, x.StateId });
        builder.HasIndex(x => x.CandidateIdentityId);

        builder.HasOne<Election>()
            .WithMany()
            .HasForeignKey(x => x.ElectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ElectoralOffice>()
            .WithMany()
            .HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CandidateIdentity>()
            .WithMany()
            .HasForeignKey(x => x.CandidateIdentityId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<PoliticalParty>()
            .WithMany()
            .HasForeignKey(x => x.PartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Federation>()
            .WithMany()
            .HasForeignKey(x => x.FederationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Coalition>()
            .WithMany()
            .HasForeignKey(x => x.CoalitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
