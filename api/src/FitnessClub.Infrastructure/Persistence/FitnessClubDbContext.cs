using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence;

public sealed class FitnessClubDbContext(DbContextOptions<FitnessClubDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FitnessClubDbContext).Assembly);
}
