using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Reports;

public sealed class ReportsApiFixture : IAsyncDisposable
{
    public static readonly TimeSpan ClubOffset = TimeSpan.FromHours(3);
    public static readonly DateTimeOffset LocalNow = Local(2026, 10, 7, 12);
    public static readonly DateOnly Today = new(2026, 10, 7);

    private readonly FitnessClubApiFactory root = new();

    public ReportsApiFixture()
    {
        Factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new ClubClock(LocalNow))));
    }

    public WebApplicationFactory<Program> Factory { get; }

    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    public async Task InUnitOfWorkAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public Task InUnitOfWorkAsync(Action<IServiceProvider> work) =>
        InUnitOfWorkAsync(services =>
        {
            work(services);
            return Task.CompletedTask;
        });

    public static DateTimeOffset Local(int year, int month, int day, int hour = 10, int minute = 0) =>
        new(year, month, day, hour, minute, 0, ClubOffset);

    public static MembershipPlan NewPlan(int validityDays, decimal price = 800m) =>
        MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(price), validityDays, null);

    public static Client NewClient(string lastName, string firstName, DateOnly dateOfBirth, DateTimeOffset registeredAt) =>
        Client.Register(PersonName.Create(firstName, lastName, null), dateOfBirth, EmailAddress.Create(TestData.UniqueEmail()), PhoneNumber.Create(UniquePhone()), registeredAt);

    public static string UniquePhone() => $"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    public async ValueTask DisposeAsync() => await root.DisposeAsync();

    private sealed class ClubClock(DateTimeOffset localNow) : TimeProvider
    {
        private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Club", ClubOffset, "Club", "Club");

        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => Zone;
    }
}
