using FitnessClub.Application.Abstractions;
using FitnessClub.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "FitnessClub";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FitnessClubDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                options.UseInMemoryDatabase(configuration["Database:InMemoryName"] ?? "FitnessClub");
            else
                options.UseSqlServer(connectionString);
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<FitnessClubDbContext>());

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

    // Applies pending EF migrations when a relational database is configured; the InMemory provider has no migrations.
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FitnessClubDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
    }
}
