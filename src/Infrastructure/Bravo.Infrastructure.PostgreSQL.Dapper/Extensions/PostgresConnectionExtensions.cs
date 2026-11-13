using Bravo.Shared.Pagination;
using Dapper;
using System.Data;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Extensions;

internal static class PostgresConnectionExtensions
{
    //use connection vs use _session? good to think about it
    //And also, is that bad to return a Tuple?
    /// <summary>
    /// Use ALWAYS the query FIRST, then the count.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="conn"></param>
    /// <param name="sql"></param>
    /// <param name="params"></param>
    /// <returns></returns>
    public static async Task<(IEnumerable<T> items, int count)> QueryPaginated<T>(
        this IDbConnection conn,
        string sql,
        object @params
        )
    {
        var multi = await conn.QueryMultipleAsync(sql, @params);
        var items = await multi.ReadAsync<T>();
        var count = await multi.ReadSingleAsync<int>();

        return (items, count);
    }
    public static async Task<PaginatedList<T>> QueryPaginated<T>(
        this IDbConnection conn,
        string sql,
        int page,
        int pageSize,
        string? search = null,
        object? filters = null
        )
    {
        var parameters = new DynamicParameters();
        parameters.Add("Page", page);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", (page - 1) * pageSize);

        if(filters is not null)
            parameters.AddDynamicParams(filters);
        
        if (search is not null)
            parameters.Add("Search", search);

        var (items, count) = await conn.QueryPaginated<T>(
            sql,
            parameters
        );

        return new PaginatedList<T>(items, count, page, pageSize);
    }
}


