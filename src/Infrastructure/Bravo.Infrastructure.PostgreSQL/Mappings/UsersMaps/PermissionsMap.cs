using Bravo.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.UsersMaps;

internal sealed class PermissionsMap : EntityMapper<Permission>
{
    protected override void ConfigureMap(EntityTypeBuilder<Permission> builder)
    {
        builder.Property(x => x.Resource)
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(x => x.Action)
              .HasMaxLength(20)
              .IsRequired();

        builder.Property(x => x.Module)
              .HasMaxLength(20)
              .IsRequired();
    }
}
