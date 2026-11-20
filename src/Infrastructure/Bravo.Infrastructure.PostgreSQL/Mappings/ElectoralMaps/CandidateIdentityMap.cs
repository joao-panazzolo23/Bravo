using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class CandidateIdentityMap : EntityMapper<CandidateIdentity>
{
    protected override void ConfigureMap(EntityTypeBuilder<CandidateIdentity> builder)
    {
        builder.ToTable("CandidateIdentities");

        builder.Property(x => x.Cpf).HasMaxLength(11);
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BirthDate);

        builder.HasIndex(x => x.Cpf).IsUnique();

        // A candidate without a CPF is identified by name and birth date, so a re-executed
        // import must map to the same row instead of inserting another one. NULLS NOT DISTINCT
        // matters here too: TSE has candidates with neither CPF nor birth date, and PostgreSQL
        // would otherwise accept unlimited duplicates for them.
        builder.HasIndex(x => new { x.FullName, x.BirthDate })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("\"Cpf\" IS NULL");
    }
}
