using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Persistence;

public class MembershipPlanRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    [Fact]
    public async Task Plan_round_trips_with_money_price()
    {
        var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(799.99m), 30, 12);
        await SaveAsync<IMembershipPlanRepository>(plans => plans.Add(plan));

        var loaded = await ReadAsync<IMembershipPlanRepository, MembershipPlan?>(plans => plans.GetByIdAsync(plan.Id, Ct));

        Assert.NotNull(loaded);
        Assert.Equal(Money.Of(799.99m), loaded.Price);
        Assert.Equal(12, loaded.VisitLimit);
    }

    [Fact]
    public async Task NameExistsAsync_ignores_case_and_excluded_plan()
    {
        var name = $"Plan {Guid.NewGuid():N}";
        var plan = MembershipPlan.Create(name, Money.Of(100m), 30, null);
        await SaveAsync<IMembershipPlanRepository>(plans => plans.Add(plan));

        Assert.True(await ReadAsync<IMembershipPlanRepository, bool>(plans => plans.NameExistsAsync($" {name.ToUpperInvariant()} ", null, Ct)));
        Assert.False(await ReadAsync<IMembershipPlanRepository, bool>(plans => plans.NameExistsAsync(name, plan.Id, Ct)));
    }

    [Fact]
    public async Task Saving_a_stale_copy_raises_conflict()
    {
        var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(100m), 30, null);
        await SaveAsync<IMembershipPlanRepository>(plans => plans.Add(plan));

        await using var stale = Factory.Services.CreateAsyncScope();
        var staleCopy = await stale.ServiceProvider.GetRequiredService<IMembershipPlanRepository>().GetByIdAsync(plan.Id, Ct);
        await ChangeAsync<IMembershipPlanRepository>(async plans => (await plans.GetByIdAsync(plan.Id, Ct))!.Deactivate());

        staleCopy!.Update(staleCopy.Name, Money.Of(150m), 30, null);

        await Assert.ThrowsAsync<ConflictException>(() => stale.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
        var current = await ReadAsync<IMembershipPlanRepository, MembershipPlan?>(plans => plans.GetByIdAsync(plan.Id, Ct));
        Assert.Equal(Money.Of(100m), current!.Price);
    }
}
