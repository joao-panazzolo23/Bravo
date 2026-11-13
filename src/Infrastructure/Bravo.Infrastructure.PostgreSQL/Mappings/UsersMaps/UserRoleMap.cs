using Bravo.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.UsersMaps;

internal sealed class UserRoleMap : EntityMapper<UserRole>
{
    protected override void ConfigureMap(EntityTypeBuilder<UserRole> builder)
    {
        builder.Property(x => x.UserId);
        builder.Property(x => x.RoleId);

        builder.HasOne(x => x.Role)
               .WithOne(x => x.UserRole);
    }
}
