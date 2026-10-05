namespace FitnessClub.Application.MembershipPlans;

public interface IMembershipPlanService
{
    Task<IReadOnlyList<MembershipPlanResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<MembershipPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<MembershipPlanResponse> CreateAsync(MembershipPlanRequest request, CancellationToken cancellationToken);

    Task<MembershipPlanResponse> UpdateAsync(Guid id, MembershipPlanRequest request, CancellationToken cancellationToken);

    Task ActivateAsync(Guid id, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
