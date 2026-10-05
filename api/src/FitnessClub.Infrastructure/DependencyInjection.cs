using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Notifications;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using FitnessClub.Domain.Visits;
using FitnessClub.Infrastructure.BackgroundJobs;
using FitnessClub.Infrastructure.Persistence;
using FitnessClub.Infrastructure.Persistence.Repositories;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitnessClub.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "FitnessClub";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FitnessClubDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                options.UseInMemoryDatabase(configuration["Database:InMemoryName"] ?? "FitnessClub");
            else
                options.UseSqlServer(connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IVisitRepository, VisitRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ITrainerRepository, TrainerRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<ITrainingSessionRepository, TrainingSessionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.AddHangfire(hangfire =>
        {
            hangfire
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();

            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                hangfire.UseInMemoryStorage();
            else
                hangfire.UseSqlServerStorage(connectionString);
        });
        services.AddHangfireServer();

        return services;
    }

    public static async Task UseInfrastructureAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapHangfireDashboard("/hangfire").AllowAnonymous();

        await InitializeDatabaseAsync(app.Services);
        RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FitnessClubDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
    }
}
