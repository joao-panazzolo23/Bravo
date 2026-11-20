using Bravo.Domain.Geography.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.GeographyMaps;

internal sealed class MunicipalityMap : EntityMapper<Municipality>
{
    protected override void ConfigureMap(EntityTypeBuilder<Municipality> builder)
    {
        builder.ToTable("Municipalities");

        builder.Property(x => x.IbgeCode)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.StateId)
            .IsRequired();

        builder.HasIndex(x => x.IbgeCode)
            .IsUnique();

        builder.HasOne(x => x.State)
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
