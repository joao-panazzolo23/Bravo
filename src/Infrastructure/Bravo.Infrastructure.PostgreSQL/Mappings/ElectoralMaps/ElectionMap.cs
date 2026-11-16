using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectionMap : EntityMapper<Election>
{
    protected override void ConfigureMap(EntityTypeBuilder<Election> builder)
    {
        builder.ToTable("Elections");

        builder.Property(x => x.TypeName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ElectionDate);

        builder.HasIndex(x => x.TseElectionCode).IsUnique();
        builder.HasIndex(x => x.Year);
    }
}
