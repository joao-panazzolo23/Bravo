using Bravo.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.UsersMaps;

internal sealed class RoleMap : EntityMapper<Role>
{
    protected override void ConfigureMap(EntityTypeBuilder<Role> builder)
    {
        builder.HasOne(x => x.UserRole)
               .WithOne(x => x.Role);

        builder.HasMany(x => x.RolePermissions)
               .WithOne(x => x.Role)
               .HasForeignKey(x => x.RoleId);
    }
}
