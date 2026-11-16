using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectionRoundMap : EntityMapper<ElectionRound>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectionRound> builder)
    {
        builder.ToTable("ElectionRounds");

        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.IsRunoff).IsRequired();

        builder.HasIndex(x => new { x.ElectionId, x.Round }).IsUnique();

        builder.HasOne<Election>()
            .WithMany()
            .HasForeignKey(x => x.ElectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
