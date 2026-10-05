using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Application.MembershipPlans;

internal sealed class MembershipPlanService(IMembershipPlanRepository plans, IUnitOfWork unitOfWork) : IMembershipPlanService
{
    public async Task<IReadOnlyList<MembershipPlanResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var list = await plans.ListAsync(includeInactive, cancellationToken);
        return list.Select(MembershipPlanResponse.FromEntity).ToList();
    }

    public async Task<MembershipPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        MembershipPlanResponse.FromEntity(await FindAsync(id, cancellationToken));

    public async Task<MembershipPlanResponse> CreateAsync(MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = MembershipPlan.Create(request.Name, Money.Of(request.Price), request.ValidityDays, request.VisitLimit);
        await EnsureNameIsUniqueAsync(request.Name, excludeId: null, cancellationToken);

        plans.Add(plan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MembershipPlanResponse.FromEntity(plan);
    }

    public async Task<MembershipPlanResponse> UpdateAsync(Guid id, MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        await EnsureNameIsUniqueAsync(request.Name, plan.Id, cancellationToken);
        plan.Update(request.Name, Money.Of(request.Price), request.ValidityDays, request.VisitLimit);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MembershipPlanResponse.FromEntity(plan);
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<MembershipPlan> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await plans.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Membership plan '{id}' was not found.");

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await plans.NameExistsAsync(name, excludeId, cancellationToken))
            throw new ConflictException($"A membership plan named '{name.Trim()}' already exists.");
    }
}
