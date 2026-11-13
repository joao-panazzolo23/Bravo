using Bravo.Infrastructure.PostgreSQL.Context;
using Bravo.Shared.Data;
using Bravo.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Bravo.Infrastructure.PostgreSQL.Data;

internal class UnityOfWork(PostgreSQLContext context) : IUnityOfWork
{
    private readonly PostgreSQLContext _context = context;

    public async Task<int> Commit()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("O registro foi alterado por outra operação. Tente novamente.");
        }
    }

    public void Rollback()
        => _context.Database.CurrentTransaction?.Rollback();
}
