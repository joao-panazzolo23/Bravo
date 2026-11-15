using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class PoliticalPartyMap : EntityMapper<PoliticalParty>
{
    protected override void ConfigureMap(EntityTypeBuilder<PoliticalParty> builder)
    {
        builder.ToTable("PoliticalParties");

        builder.Property(x => x.Number).IsRequired();
        builder.Property(x => x.Acronym).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => x.Number).IsUnique();
    }
}
