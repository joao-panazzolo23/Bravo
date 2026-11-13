using Bravo.Domain.Users.Entities;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.UsersMaps;

internal sealed class UserMap : EntityMapper<User>
{
    protected override void ConfigureMap(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.Name);
        builder.Property(x => x.Email);
        builder.Property(x => x.Document);
        builder.Property(x => x.Phone);
        builder.Property(x => x.Password);
        builder.Property(x => x.RoleType).HasConversion<string>();
        builder.Property(x => x.IsActive);
        builder.Property(x => x.SecurityStamp);

        builder.HasMany(x => x.UserRoles)
               .WithOne(x => x.User)
               .HasForeignKey(x => x.UserId);
    }
}
