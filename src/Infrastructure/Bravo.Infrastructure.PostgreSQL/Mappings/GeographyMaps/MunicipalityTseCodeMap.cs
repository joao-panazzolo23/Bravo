using Bravo.Domain.Geography.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.GeographyMaps;

internal sealed class MunicipalityTseCodeMap : EntityMapper<MunicipalityTseCode>
{
    protected override void ConfigureMap(EntityTypeBuilder<MunicipalityTseCode> builder)
    {
        builder.ToTable("MunicipalityTseCodes");

        builder.Property(x => x.StateId).IsRequired();
        builder.Property(x => x.TseCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.IbgeCode).IsRequired();
        builder.Property(x => x.MunicipalityId).IsRequired();
        builder.Property(x => x.TseName).HasMaxLength(250);
        builder.Property(x => x.MesoRegionName).HasMaxLength(250);
        builder.Property(x => x.MicroRegionName).HasMaxLength(250);

        builder.HasIndex(x => x.TseCode).IsUnique();
        builder.HasIndex(x => x.IbgeCode);
        builder.HasIndex(x => new { x.StateId, x.TseCode });

        builder.HasOne<State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Municipality>()
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
