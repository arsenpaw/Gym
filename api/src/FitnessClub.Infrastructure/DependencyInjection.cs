using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Notifications;
using FitnessClub.Application.Reports;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Notifications;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using FitnessClub.Domain.Visits;
using FitnessClub.Infrastructure.BackgroundJobs;
using FitnessClub.Infrastructure.Notifications;
using FitnessClub.Infrastructure.Persistence;
using FitnessClub.Infrastructure.Persistence.Repositories;
using FitnessClub.Infrastructure.Reports;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FitnessClub.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "FitnessClub";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FitnessClubDbContext>(options =>
            options.UseSqlServer(
                RequiredConnectionString(configuration),
                sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IVisitRepository, VisitRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ITrainerRepository, TrainerRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<ITrainingSessionRepository, TrainingSessionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IReportQueries, ReportQueries>();

        services.AddOptions<ExpiryNotificationOptions>()
            .Bind(configuration.GetSection(ExpiryNotificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<ExpiryNotificationOptions>>().Value);
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                smtp => !smtp.IsConfigured || !string.IsNullOrWhiteSpace(smtp.FromAddress),
                $"{SmtpOptions.SectionName}:FromAddress is required when {SmtpOptions.SectionName}:Host is set.")
            .ValidateOnStart();
        if (string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.SectionName}:Host"]))
            services.AddScoped<INotificationSender, LoggingNotificationSender>();
        else
            services.AddScoped<INotificationSender, SmtpNotificationSender>();
        services.AddScoped<ExpiryNotificationJob>();

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(RequiredConnectionString(configuration)));
        services.AddHangfireServer();

        return services;
    }

    public static async Task UseInfrastructureAsync(this WebApplication app)
    {
        await InitializeDatabaseAsync(app.Services);

        if (app.Environment.IsDevelopment())
            app.MapHangfireDashboard("/hangfire").AllowAnonymous();

        RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<FitnessClubDbContext>().Database.MigrateAsync();
    }

    private static string RequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not set. Start SQL Server with "
                + "'docker compose -f deploy/docker-compose.yml up -d db' and set the connection string in user secrets (see api/CLAUDE.md).");

        return connectionString;
    }
}
