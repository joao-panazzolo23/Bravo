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
    }
}
