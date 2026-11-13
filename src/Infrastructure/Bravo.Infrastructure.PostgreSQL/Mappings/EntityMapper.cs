using Bravo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bravo.Infrastructure.PostgreSQL.Mappings;

internal abstract class EntityMapper<T> : IEntityTypeConfiguration<T> where T : Entity
{
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedAt);
        ConfigureMap(builder);
    }
    protected abstract void ConfigureMap(EntityTypeBuilder<T> builder);
}