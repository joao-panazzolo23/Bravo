using Bravo.Infrastructure.PostgreSQL.Context;
using Bravo.Shared.Data;
using Bravo.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            // A failed save leaves the tracked graph in an unusable state. Clearing it lets the
            // caller record the failure (or retry the operation) instead of re-submitting the
            // same rejected changes.
            _context.ChangeTracker.Clear();
            throw new ConcurrencyException("O registro foi alterado por outra operação. Tente novamente.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } unique)
        {
            // A unique violation is surfaced as a concurrency conflict so the Application layer
            // can react to it without depending on Npgsql. The import overlap guard relies on
            // this translation.
            _context.ChangeTracker.Clear();
            throw new ConcurrencyException(
                $"Um registro com a mesma chave já existe ({unique.ConstraintName}).");
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public void Rollback()
        => _context.Database.CurrentTransaction?.Rollback();
}
