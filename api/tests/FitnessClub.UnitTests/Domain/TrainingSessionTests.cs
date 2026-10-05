using FitnessClub.Domain.Common;
using FitnessClub.Domain.Training;
using FitnessClub.UnitTests.Fakes;

namespace FitnessClub.UnitTests.Domain;

public class TrainingSessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly InMemoryTrainingSessionRepository _repository = new();
    private readonly SessionScheduler _scheduler;

    public TrainingSessionTests()
    {
        _scheduler = new SessionScheduler(_repository);
    }

    private Task<Booking> BookAsync(TrainingSession session, FitnessClub.Domain.Clients.Client client, DateTimeOffset? now = null) =>
        _scheduler.BookAsync(session, client, now ?? TestData.Now, Ct);

    private Task<TrainingSession> GroupSessionAsync(int capacity = 2) =>
        _scheduler.ScheduleAsync("Morning yoga", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(), capacity, TestData.Now, Ct);

    [Fact]
    public async Task Schedule_creates_scheduled_session()
    {
        var trainer = TestData.Trainer();
        var room = TestData.Room(capacity: 20);
        var slot = TestData.Slot();

        var session = await _scheduler.ScheduleAsync(" Morning yoga ", SessionType.Group, trainer, room, slot, 15, TestData.Now, Ct);

        Assert.Equal("Morning yoga", session.Title);
        Assert.Equal(trainer.Id, session.TrainerId);
        Assert.Equal(room.Id, session.RoomId);
        Assert.Equal(slot, session.Slot);
        Assert.Equal(15, session.Capacity);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
    }

    [Fact]
    public async Task Schedule_in_the_past_throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(daysFromToday: -1), 5, TestData.Now, Ct));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(241)]
    public async Task Schedule_with_duration_out_of_range_throws(int minutes)
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(durationMinutes: minutes), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_outside_trainer_hours_throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(startHour: 19, durationMinutes: 90), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_in_inactive_room_throws()
    {
        var room = TestData.Room();
        room.Deactivate();

        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), room, TestData.Slot(), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_with_capacity_above_room_capacity_throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), TestData.Room(capacity: 10), TestData.Slot(), 11, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_individual_session_with_more_than_one_place_throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Personal", SessionType.Individual, TestData.Trainer(), TestData.Room(), TestData.Slot(), 2, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_when_trainer_is_busy_throws()
    {
        var repository = new InMemoryTrainingSessionRepository();
        var scheduler = new SessionScheduler(repository);
        var trainer = TestData.Trainer();
        repository.Add(await scheduler.ScheduleAsync("Yoga", SessionType.Group, trainer, TestData.Room(), TestData.Slot(startHour: 10), 5, TestData.Now, Ct));

        await Assert.ThrowsAsync<DomainException>(() => scheduler.ScheduleAsync(
            "Pilates", SessionType.Group, trainer, TestData.Room(), TestData.Slot(startHour: 10, durationMinutes: 30), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_when_room_is_taken_throws()
    {
        var repository = new InMemoryTrainingSessionRepository();
        var scheduler = new SessionScheduler(repository);
        var room = TestData.Room();
        repository.Add(await scheduler.ScheduleAsync("Yoga", SessionType.Group, TestData.Trainer(), room, TestData.Slot(startHour: 10), 5, TestData.Now, Ct));

        await Assert.ThrowsAsync<DomainException>(() => scheduler.ScheduleAsync(
            "Pilates", SessionType.Group, TestData.Trainer(), room, TestData.Slot(startHour: 10, durationMinutes: 30), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_after_a_cancelled_session_in_the_same_room_is_allowed()
    {
        var repository = new InMemoryTrainingSessionRepository();
        var scheduler = new SessionScheduler(repository);
        var room = TestData.Room();
        var cancelled = await scheduler.ScheduleAsync("Yoga", SessionType.Group, TestData.Trainer(), room, TestData.Slot(), 5, TestData.Now, Ct);
        cancelled.Cancel(TestData.Now);
        repository.Add(cancelled);

        var session = await scheduler.ScheduleAsync("Pilates", SessionType.Group, TestData.Trainer(), room, TestData.Slot(), 5, TestData.Now, Ct);

        Assert.Equal(SessionStatus.Scheduled, session.Status);
    }

    [Fact]
    public async Task Book_adds_active_booking()
    {
        var session = await GroupSessionAsync();
        var client = TestData.ClientWithMembership();

        var booking = await BookAsync(session, client);

        Assert.Equal(client.Id, booking.ClientId);
        Assert.Equal(1, session.ActiveBookingCount);
    }

    [Fact]
    public async Task Book_without_membership_on_session_date_throws()
    {
        var session = await GroupSessionAsync();

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, TestData.Client()));
    }

    [Fact]
    public async Task Book_same_client_twice_throws()
    {
        var session = await GroupSessionAsync();
        var client = TestData.ClientWithMembership();
        await BookAsync(session, client);

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, client));
    }

    [Fact]
    public async Task Book_when_full_throws()
    {
        var session = await GroupSessionAsync(capacity: 1);
        await BookAsync(session, TestData.ClientWithMembership());

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, TestData.ClientWithMembership()));
    }

    [Fact]
    public async Task Book_after_session_started_throws()
    {
        var session = await GroupSessionAsync();

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, TestData.ClientWithMembership(), session.Slot.Start));
    }

    [Fact]
    public async Task CancelBooking_frees_the_place()
    {
        var session = await GroupSessionAsync(capacity: 1);
        var client = TestData.ClientWithMembership();
        await BookAsync(session, client);

        session.CancelBooking(client.Id, TestData.Now);

        Assert.Equal(0, session.ActiveBookingCount);
        await BookAsync(session, TestData.ClientWithMembership());
    }

    [Fact]
    public async Task CancelBooking_for_client_without_booking_throws()
    {
        var session = await GroupSessionAsync();

        Assert.Throws<DomainException>(() => session.CancelBooking(Guid.NewGuid(), TestData.Now));
    }

    [Fact]
    public async Task Cancelled_session_rejects_bookings_and_second_cancel()
    {
        var session = await GroupSessionAsync();
        await BookAsync(session, TestData.ClientWithMembership());

        session.Cancel(TestData.Now);

        Assert.Equal(SessionStatus.Cancelled, session.Status);
        Assert.Equal(0, session.ActiveBookingCount);
        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, TestData.ClientWithMembership()));
        Assert.Throws<DomainException>(() => session.Cancel(TestData.Now));
    }

    [Fact]
    public async Task Book_client_into_overlapping_session_throws()
    {
        var client = TestData.ClientWithMembership();
        var first = await GroupSessionAsync();
        await BookAsync(first, client);
        _repository.Add(first);
        var overlapping = await _scheduler.ScheduleAsync(
            "Pilates", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(startHour: 10, durationMinutes: 30), 5, TestData.Now, Ct);

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(overlapping, client));
    }
}
