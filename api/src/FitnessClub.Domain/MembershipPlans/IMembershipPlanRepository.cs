using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.MembershipPlans;

public interface IMembershipPlanRepository : IRepository<MembershipPlan>
{
    Task<IReadOnlyList<MembershipPlan>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);
}
