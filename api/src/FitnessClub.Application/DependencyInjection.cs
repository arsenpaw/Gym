using FitnessClub.Application.Clients;
using FitnessClub.Application.MembershipPlans;
using FitnessClub.Application.Notifications;
using FitnessClub.Application.Reports;
using FitnessClub.Application.Rooms;
using FitnessClub.Application.Trainers;
using FitnessClub.Application.Training;
using FitnessClub.Domain.Training;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMembershipPlanService, MembershipPlanService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ITrainerService, TrainerService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<ITrainingSessionService, TrainingSessionService>();
        services.AddScoped<ISessionScheduler, SessionScheduler>();
        services.AddScoped<IExpiryNotificationService, ExpiryNotificationService>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
