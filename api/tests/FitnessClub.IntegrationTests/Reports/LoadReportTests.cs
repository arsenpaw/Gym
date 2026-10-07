using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.Reports;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using Microsoft.Extensions.DependencyInjection;
using static FitnessClub.IntegrationTests.Reports.ReportsApiFixture;

namespace FitnessClub.IntegrationTests.Reports;

public class LoadReportTests(ReportsApiFixture fixture) : IClassFixture<ReportsApiFixture>
{
    private const string Url = "/api/reports/load";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Trainer NewTrainer(string lastName, string firstName)
    {
        var trainer = Trainer.Hire(PersonName.Create(firstName, lastName, null), PhoneNumber.Create(UniquePhone()), null, "Fitness");
        trainer.SetWorkingHours(Enum.GetValues<DayOfWeek>().Select(day => WorkingHours.Create(day, new TimeOnly(8, 0), new TimeOnly(20, 0))));
        return trainer;
    }

    private static TimeSlot Slot(int day, int hour, int minute, int durationMinutes)
    {
        var start = Local(2026, 11, day, hour, minute);
        return TimeSlot.Create(start, start.AddMinutes(durationMinutes));
    }

    [Fact]
    public async Task Reports_trainer_and_room_load_per_club_day_for_scheduled_sessions()
    {
        var bondar = NewTrainer("Bondar", "Taras");
        var antonenko = NewTrainer("Antonenko", "Ivan");
        var bigRoom = Room.Create($"A-Room {Guid.NewGuid():N}", 20);
        var smallRoom = Room.Create($"B-Room {Guid.NewGuid():N}", 5);
        var plan = NewPlan(validityDays: 365);
        var clients = Enumerable.Range(0, 3)
            .Select(i => NewClient("Member", $"Number{i}", new DateOnly(1990, 1, 1), LocalNow))
            .ToList();
        foreach (var client in clients)
            client.PurchaseMembership(plan, Today, PaymentMethod.Cash, LocalNow);
        var (x, y, z) = (clients[0], clients[1], clients[2]);

        await fixture.InUnitOfWorkAsync(services =>
        {
            services.GetRequiredService<ITrainerRepository>().Add(bondar);
            services.GetRequiredService<ITrainerRepository>().Add(antonenko);
            services.GetRequiredService<IRoomRepository>().Add(bigRoom);
            services.GetRequiredService<IRoomRepository>().Add(smallRoom);
            services.GetRequiredService<IMembershipPlanRepository>().Add(plan);
            foreach (var client in clients)
                services.GetRequiredService<IClientRepository>().Add(client);
        });

        await fixture.InUnitOfWorkAsync(async services =>
        {
            var scheduler = services.GetRequiredService<ISessionScheduler>();
            var sessions = services.GetRequiredService<ITrainingSessionRepository>();

            var morning = await scheduler.ScheduleAsync("Yoga", SessionType.Group, bondar, bigRoom, Slot(2, 10, 0, 60), 10, LocalNow, Ct);
            await scheduler.BookAsync(morning, x, LocalNow, Ct);
            await scheduler.BookAsync(morning, y, LocalNow, Ct);
            await scheduler.BookAsync(morning, z, LocalNow, Ct);
            morning.CancelBooking(z.Id, LocalNow);

            var noon = await scheduler.ScheduleAsync("Pilates", SessionType.Group, bondar, smallRoom, Slot(2, 12, 0, 90), 3, LocalNow, Ct);
            await scheduler.BookAsync(noon, x, LocalNow, Ct);

            var personal = await scheduler.ScheduleAsync("Personal", SessionType.Individual, antonenko, bigRoom, Slot(3, 10, 0, 45), 1, LocalNow, Ct);
            await scheduler.BookAsync(personal, y, LocalNow, Ct);

            var cancelled = await scheduler.ScheduleAsync("Boxing", SessionType.Group, antonenko, smallRoom, Slot(2, 15, 0, 60), 5, LocalNow, Ct);
            await scheduler.BookAsync(cancelled, z, LocalNow, Ct);
            cancelled.Cancel(LocalNow);

            var outside = await scheduler.ScheduleAsync("Yoga", SessionType.Group, bondar, bigRoom, Slot(6, 10, 0, 60), 10, LocalNow, Ct);

            foreach (var session in new[] { morning, noon, personal, cancelled, outside })
                sessions.Add(session);
        });

        var report = await fixture.CreateClientWithRoles(Roles.Admin)
            .GetFromJsonAsync<LoadReportResponse>($"{Url}?from=2026-11-02&to=2026-11-04", Ct);

        Assert.NotNull(report);
        Assert.Equal(new DateOnly(2026, 11, 2), report.From);
        Assert.Equal(new DateOnly(2026, 11, 4), report.To);
        Assert.Equal(
            [new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4)],
            report.Days.Select(day => day.Date));

        var monday = report.Days[0];
        Assert.Equal([new TrainerLoad(bondar.Id, "Bondar Taras", 2, 2.5m, 2)], monday.Trainers);
        Assert.Equal(
            [
                new RoomLoad(bigRoom.Id, bigRoom.Name, 1, 1m, 2, 10, 20m),
                new RoomLoad(smallRoom.Id, smallRoom.Name, 1, 1.5m, 1, 3, 33.3m),
            ],
            monday.Rooms);

        var tuesday = report.Days[1];
        Assert.Equal([new TrainerLoad(antonenko.Id, "Antonenko Ivan", 1, 0.75m, 1)], tuesday.Trainers);
        Assert.Equal([new RoomLoad(bigRoom.Id, bigRoom.Name, 1, 0.75m, 1, 1, 100m)], tuesday.Rooms);

        var wednesday = report.Days[2];
        Assert.Empty(wednesday.Trainers);
        Assert.Empty(wednesday.Rooms);
    }

    [Fact]
    public async Task Default_range_is_the_week_starting_today()
    {
        var report = await fixture.CreateClientWithRoles(Roles.Admin).GetFromJsonAsync<LoadReportResponse>(Url, Ct);

        Assert.NotNull(report);
        Assert.Equal(Today, report.From);
        Assert.Equal(Today.AddDays(6), report.To);
        Assert.Equal(7, report.Days.Count);
    }

    [Fact]
    public async Task Range_of_93_days_is_allowed()
    {
        var report = await fixture.CreateClientWithRoles(Roles.Admin)
            .GetFromJsonAsync<LoadReportResponse>($"{Url}?from=2026-01-01&to=2026-04-03", Ct);

        Assert.NotNull(report);
        Assert.Equal(93, report.Days.Count);
    }

    [Theory]
    [InlineData("?from=2026-11-04&to=2026-11-02")]
    [InlineData("?from=2026-01-01&to=2026-04-04")]
    [InlineData("?from=yesterday")]
    public async Task Invalid_range_returns_400_problem_details(string query)
    {
        var response = await fixture.CreateClientWithRoles(Roles.Admin).GetAsync($"{Url}{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
