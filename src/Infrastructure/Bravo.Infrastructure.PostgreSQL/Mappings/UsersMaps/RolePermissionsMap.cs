using Bravo.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.UsersMaps;

internal class RolePermissionsMap : EntityMapper<RolePermission>
{
    protected override void ConfigureMap(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasOne(x => x.Role)
               .WithMany(x => x.RolePermissions)
               .HasForeignKey(x => x.RoleId);
    }
}
