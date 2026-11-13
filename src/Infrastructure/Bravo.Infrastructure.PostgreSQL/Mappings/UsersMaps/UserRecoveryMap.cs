using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Bravo.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings.UsersMaps;

internal sealed class UserRecoveryMap : EntityMapper<UserRecovery>
{
    protected override void ConfigureMap(EntityTypeBuilder<UserRecovery> builder)
    {
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
           v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
           v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
       );

        builder.Property(x => x.TokenHash)
               .HasMaxLength(64)
               .IsRequired();

        builder.Property(x => x.ExpiresAt).HasConversion(dateTimeConverter);
        builder.Property(x => x.UsedAt).HasConversion(dateTimeConverter);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.UsedAt });

        builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId);
    }
}
