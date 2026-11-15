using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectionRoundMap : EntityMapper<ElectionRound>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectionRound> builder)
    {
        builder.ToTable("ElectionRounds");

        builder.Property(x => x.ElectionId).IsRequired();
        builder.Property(x => x.Number).IsRequired();

        builder.HasIndex(x => new { x.ElectionId, x.Number }).IsUnique();

        builder.HasMany(x => x.Contests)
            .WithOne(x => x.ElectionRound)
            .HasForeignKey(x => x.ElectionRoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
