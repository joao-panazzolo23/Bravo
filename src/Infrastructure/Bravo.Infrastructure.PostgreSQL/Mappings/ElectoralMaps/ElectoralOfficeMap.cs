using Bravo.Domain.Electoral.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.ElectoralMaps;

internal sealed class ElectoralOfficeMap : EntityMapper<ElectoralOffice>
{
    protected override void ConfigureMap(EntityTypeBuilder<ElectoralOffice> builder)
    {
        builder.ToTable("ElectoralOffices");

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.TseOfficeCode).IsUnique();
    }
}
