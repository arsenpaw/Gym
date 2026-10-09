using FitnessClub.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class VisitRepository(FitnessClubDbContext db) : Repository<Visit>(db), IVisitRepository
{
    public async Task<IReadOnlyList<Visit>> ListForClientAsync(Guid clientId, int skip, int take, CancellationToken cancellationToken) =>
        await Set
            .Where(v => v.ClientId == clientId)
            .OrderByDescending(v => v.CheckedInAt)
            .ThenByDescending(v => v.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        Set.CountAsync(v => v.ClientId == clientId, cancellationToken);
}
