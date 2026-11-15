using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectionMap : EntityMapper<Election>
{
    protected override void ConfigureMap(EntityTypeBuilder<Election> builder)
    {
        builder.ToTable("Elections");

        builder.Property(x => x.TseCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Year).IsRequired();
        builder.Property(x => x.TypeCode).IsRequired();
        builder.Property(x => x.TypeName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.ElectionDate).IsRequired();

        builder.HasIndex(x => x.TseCode).IsUnique();
        builder.HasIndex(x => x.Year);

        builder.HasMany(x => x.Rounds)
            .WithOne(x => x.Election)
            .HasForeignKey(x => x.ElectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
