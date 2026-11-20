using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class FederationMap : EntityMapper<Federation>
{
    protected override void ConfigureMap(EntityTypeBuilder<Federation> builder)
    {
        builder.ToTable("Federations");

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Acronym).IsRequired();
        builder.Property(x => x.Composition);

        builder.HasIndex(x => x.TseFederationNumber).IsUnique();
    }
}
