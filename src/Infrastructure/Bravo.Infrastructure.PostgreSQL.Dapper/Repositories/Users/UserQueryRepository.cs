using Bravo.Domain.Users.Repositories;
using Bravo.Domain.Users.ViewModel;
using Bravo.Infrastructure.PostgreSQL.Dapper.Sessions;
using Dapper;

namespace Bravo.Infrastructure.PostgreSQL.Dapper.Repositories.Users;

internal class UserQueryRepository(PostgreSQLSession session) : IUserQueryRepository
{
    private readonly PostgreSQLSession _session = session;

    public async Task<GetUserViewModel?> GetById(Guid id)
    {
        var sql = @"
                SELECT
                    U.""Id"",
                    U.""Name"",
                    U.""Email"",
                    U.""Document"",
                    U.""Phone"",
                    U.""RoleType"",
                    U.""IsActive""
                FROM
                    ""Users"" AS U
                WHERE
                    U.""Id"" = @Id
            ";
        return await _session.Connection.QueryFirstOrDefaultAsync<GetUserViewModel>(sql, new
        {
            Id = id
        });
    }

    public async Task<IEnumerable<ListUserViewModel?>> List(int page, int pageSize)
    {
        var sql = @"
                SELECT
                    U.""Id"",
                    U.""Name"",
                    U.""Email"",
                    U.""Document"",
                    U.""Phone"",
                    U.""RoleType"",
                    U.""IsActive""
                FROM
                    ""Users"" AS U
                LIMIT
                    @PageSize
                OFFSET
                    @OffSet;
            ";

        return await _session.Connection.QueryAsync<ListUserViewModel>(sql, new
        {
            PageSize = pageSize,
            OffSet = (page - 1) * pageSize
        });
    }

    public async Task<GetUserViewModel?> GetByEmail(string email)
    {
        var sql = @"
                SELECT
                    U.""Id"",
                    U.""Name"",
                    U.""Email"",
                    U.""Document"",
                    U.""Phone"",
                    U.""RoleType"",
                    U.""IsActive""
                FROM
                    ""Users"" AS U             
                WHERE
                    U.""Email"" = @Email
                LIMIT 1;
            ";
        return await _session.Connection.QueryFirstOrDefaultAsync<GetUserViewModel>(sql, new
        {
            Email = email
        });
    }

}
