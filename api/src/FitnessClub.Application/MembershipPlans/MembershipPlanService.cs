using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Application.MembershipPlans;

public sealed class MembershipPlanService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<MembershipPlanResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = db.MembershipPlans.AsNoTracking();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        var plans = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return plans.Select(MembershipPlanResponse.FromEntity).ToList();
    }

    public async Task<MembershipPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        MembershipPlanResponse.FromEntity(await FindAsync(id, cancellationToken));

    public async Task<MembershipPlanResponse> CreateAsync(MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = MembershipPlan.Create(request.Name, request.Price, request.ValidityDays, request.VisitLimit);
        await EnsureNameIsUniqueAsync(plan.Name, excludeId: null, cancellationToken);

        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return MembershipPlanResponse.FromEntity(plan);
    }

    public async Task<MembershipPlanResponse> UpdateAsync(Guid id, MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Update(request.Name, request.Price, request.ValidityDays, request.VisitLimit);
        await EnsureNameIsUniqueAsync(plan.Name, plan.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return MembershipPlanResponse.FromEntity(plan);
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Activate();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<MembershipPlan> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new NotFoundException($"Membership plan '{id}' was not found.");

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.ToLower();
        var nameTaken = await db.MembershipPlans.AnyAsync(
            p => p.Id != excludeId && p.Name.ToLower() == normalizedName,
            cancellationToken);

        if (nameTaken)
            throw new ConflictException($"A membership plan named '{name}' already exists.");
    }
}
