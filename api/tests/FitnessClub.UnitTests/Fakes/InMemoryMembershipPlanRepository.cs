using FitnessClub.Domain.MembershipPlans;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryMembershipPlanRepository : IMembershipPlanRepository
{
    private readonly List<MembershipPlan> _plans = [];

    public Task<MembershipPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_plans.FirstOrDefault(p => p.Id == id));

    public void Add(MembershipPlan aggregate) => _plans.Add(aggregate);

    public Task<IReadOnlyList<MembershipPlan>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MembershipPlan>>(
            _plans.Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToList());

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(_plans.Any(p => p.Id != excludeId && string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));
}
