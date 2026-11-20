using Bravo.Domain.Geography.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.GeographyMaps;

internal sealed class StateMap : EntityMapper<State>
{
    protected override void ConfigureMap(EntityTypeBuilder<State> builder)
    {
        builder.ToTable("States");

        builder.Property(x => x.IbgeCode)
            .IsRequired();

        builder.Property(x => x.Abbreviation)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.Region)
            .IsRequired();

        builder.HasIndex(x => x.IbgeCode)
            .IsUnique();

        builder.HasIndex(x => x.Abbreviation)
            .IsUnique();
    }
}
