using FitnessClub.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class VisitRepository(FitnessClubDbContext db) : Repository<Visit>(db), IVisitRepository
{
    public async Task<IReadOnlyList<Visit>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await Set.Where(v => v.ClientId == clientId).OrderByDescending(v => v.CheckedInAt).ToListAsync(cancellationToken);
}
