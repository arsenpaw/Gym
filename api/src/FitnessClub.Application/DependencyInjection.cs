using FitnessClub.Application.MembershipPlans;
using FitnessClub.Domain.Training;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMembershipPlanService, MembershipPlanService>();
        services.AddScoped<ISessionScheduler, SessionScheduler>();
        return services;
    }
}
