using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class PoliticalPartyMap : EntityMapper<PoliticalParty>
{
    protected override void ConfigureMap(EntityTypeBuilder<PoliticalParty> builder)
    {
        builder.ToTable("PoliticalParties");

        builder.Property(x => x.Acronym).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => new { x.TsePartyNumber, x.Acronym }).IsUnique();
    }
}
