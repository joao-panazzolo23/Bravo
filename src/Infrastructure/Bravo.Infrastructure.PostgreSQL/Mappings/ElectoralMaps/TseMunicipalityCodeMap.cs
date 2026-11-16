using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class TseMunicipalityCodeMap : EntityMapper<TseMunicipalityCode>
{
    protected override void ConfigureMap(EntityTypeBuilder<TseMunicipalityCode> builder)
    {
        builder.ToTable("TseMunicipalityCodes");

        builder.Property(x => x.RawCode).HasMaxLength(10).IsRequired();

        builder.Property(x => x.MatchMethod)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Confidence).HasPrecision(3, 2).IsRequired();
        builder.Property(x => x.IsVerified).IsRequired();
        builder.Property(x => x.VerifiedAt);

        builder.HasIndex(x => new { x.RawCode, x.ElectionYear }).IsUnique();
        builder.HasIndex(x => x.MunicipalityId);
        builder.HasIndex(x => new { x.IsVerified, x.Confidence });

        builder.HasOne<Bravo.Domain.Geography.Entities.State>()
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Bravo.Domain.Geography.Entities.Municipality>()
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
