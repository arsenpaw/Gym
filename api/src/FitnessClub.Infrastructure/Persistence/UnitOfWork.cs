using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence;

internal sealed class UnitOfWork(FitnessClubDbContext db) : IUnitOfWork
{
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
    }
}
