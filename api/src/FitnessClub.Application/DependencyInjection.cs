using FitnessClub.Application.MembershipPlans;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<MembershipPlanService>();
        return services;
    }
}
