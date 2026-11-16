using System.Data;
using Dapper;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.TypeHandlers;

/// <summary>
/// Dapper has no built-in binding for <see cref="DateOnly"/>, which the electoral schema uses
/// for source dates. This handler maps it to PostgreSQL <c>date</c>.
/// </summary>
internal sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly dateOnly => dateOnly,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.Parse(text),
        _ => throw new DataException($"Cannot convert '{value}' to DateOnly.")
    };
}

internal sealed class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly? value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
    }

    public override DateOnly? Parse(object value) => value switch
    {
        null or DBNull => null,
        DateOnly dateOnly => dateOnly,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.Parse(text),
        _ => throw new DataException($"Cannot convert '{value}' to DateOnly.")
    };
}
