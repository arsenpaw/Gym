using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Persistence;

public class TrainingSessionRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    private static readonly DateTimeOffset SessionStart = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    private async Task<(Trainer Trainer, Room Room)> SeedTrainerAndRoomAsync()
    {
        var trainer = Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create(UniquePhone()), null, "Yoga");
        trainer.SetWorkingHours(Enum.GetValues<DayOfWeek>().Select(day => WorkingHours.Create(day, new TimeOnly(8, 0), new TimeOnly(20, 0))));
        var room = Room.Create($"Room {Guid.NewGuid():N}", 20);
        await SaveAsync<ITrainerRepository>(trainers => trainers.Add(trainer));
        await SaveAsync<IRoomRepository>(rooms => rooms.Add(room));
        return (trainer, room);
    }

    private static TimeSlot Slot(int startOffsetMinutes = 0, int durationMinutes = 60) =>
        TimeSlot.Create(SessionStart.AddMinutes(startOffsetMinutes), SessionStart.AddMinutes(startOffsetMinutes + durationMinutes));

    [Fact]
    public async Task Session_round_trips_with_slot_and_bookings()
    {
        var (trainer, room) = await SeedTrainerAndRoomAsync();
        var client = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), null, Now);
        client.PurchaseMembership(MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(800m), 30, null), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        TrainingSession session = null!;
        await ChangeAsync<ITrainingSessionRepository>(async sessions =>
        {
            var scheduler = new SessionScheduler(sessions);
            session = await scheduler.ScheduleAsync("Yoga", SessionType.Group, trainer, room, Slot(), 10, Now, Ct);
            await scheduler.BookAsync(session, client, Now, Ct);
            sessions.Add(session);
        });

        var loaded = await ReadAsync<ITrainingSessionRepository, TrainingSession?>(sessions => sessions.GetByIdAsync(session.Id, Ct));

        Assert.NotNull(loaded);
        Assert.Equal(Slot(), loaded.Slot);
        Assert.Equal(client.Id, Assert.Single(loaded.Bookings).ClientId);
        Assert.True(await ReadAsync<ITrainingSessionRepository, bool>(s => s.ClientHasBookingDuringAsync(client.Id, Slot(30), Ct)));
        Assert.False(await ReadAsync<ITrainingSessionRepository, bool>(s => s.ClientHasBookingDuringAsync(client.Id, Slot(60), Ct)));
    }

    [Fact]
    public async Task Overlap_queries_detect_busy_trainer_and_room_but_ignore_cancelled_sessions()
    {
        var (trainer, room) = await SeedTrainerAndRoomAsync();
        var (otherTrainer, otherRoom) = await SeedTrainerAndRoomAsync();
        await ChangeAsync<ITrainingSessionRepository>(async sessions =>
        {
            var scheduler = new SessionScheduler(sessions);
            sessions.Add(await scheduler.ScheduleAsync("Yoga", SessionType.Group, trainer, room, Slot(), 10, Now, Ct));
            var cancelled = await scheduler.ScheduleAsync("Pilates", SessionType.Group, otherTrainer, otherRoom, Slot(), 10, Now, Ct);
            cancelled.Cancel(Now);
            sessions.Add(cancelled);
        });

        Assert.True(await ReadAsync<ITrainingSessionRepository, bool>(s => s.TrainerHasSessionDuringAsync(trainer.Id, Slot(30), Ct)));
        Assert.True(await ReadAsync<ITrainingSessionRepository, bool>(s => s.RoomIsBookedDuringAsync(room.Id, Slot(30), Ct)));
        Assert.False(await ReadAsync<ITrainingSessionRepository, bool>(s => s.TrainerHasSessionDuringAsync(trainer.Id, Slot(60), Ct)));
        Assert.False(await ReadAsync<ITrainingSessionRepository, bool>(s => s.TrainerHasSessionDuringAsync(otherTrainer.Id, Slot(), Ct)));
        Assert.False(await ReadAsync<ITrainingSessionRepository, bool>(s => s.RoomIsBookedDuringAsync(otherRoom.Id, Slot(), Ct)));

        var forTrainer = await ReadAsync<ITrainingSessionRepository, IReadOnlyList<TrainingSession>>(
            s => s.ListForTrainerStartingBetweenAsync(trainer.Id, SessionStart.AddHours(-1), SessionStart.AddHours(1), Ct));
        Assert.Single(forTrainer);
    }
}
