using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class MembershipPlanRepository(FitnessClubDbContext db)
    : Repository<MembershipPlan>(db), IMembershipPlanRepository
{
    public async Task<IReadOnlyList<MembershipPlan>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await Set.Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLower();
        return Set.AnyAsync(p => p.Id != excludeId && p.Name.ToLower() == normalizedName, cancellationToken);
    }
}
