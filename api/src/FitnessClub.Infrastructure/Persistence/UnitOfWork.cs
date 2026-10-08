using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence;

internal sealed class UnitOfWork(FitnessClubDbContext db) : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The data was changed by someone else. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new ConflictException("This would duplicate an existing record. Reload the data and try again.");
        }
    }
}
