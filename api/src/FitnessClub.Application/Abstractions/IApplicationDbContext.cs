using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<MembershipPlan> MembershipPlans { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
