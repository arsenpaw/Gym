using FitnessClub.Application.Common;
using FitnessClub.Application.Training;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;

namespace FitnessClub.IntegrationTests.Services;

public class TrainingSessionServiceTests : ServiceTestBase
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _clock = new(TestData.Now);
    private readonly TrainingSessionService _service;

    public TrainingSessionServiceTests(FitnessClubApiFactory factory) : base(factory)
    {
        _service = new TrainingSessionService(
            Get<ITrainingSessionRepository>(), Get<ITrainerRepository>(), Get<IRoomRepository>(), Get<IClientRepository>(),
            Get<ISessionScheduler>(), UnitOfWork, _clock);
    }

    private async Task<Trainer> AddTrainerAsync()
    {
        var trainer = TestData.Trainer();
        Get<ITrainerRepository>().Add(trainer);
        await SeedAsync();
        return trainer;
    }

    private async Task<Room> AddRoomAsync(int capacity = 20)
    {
        var room = TestData.Room(capacity);
        Get<IRoomRepository>().Add(room);
        await SeedAsync();
        return room;
    }

    private async Task<Guid> AddClientWithMembershipAsync()
    {
        var client = TestData.Client();
        TestData.Buy(client, await SeedPlanAsync());
        Get<IClientRepository>().Add(client);
        await SeedAsync();
        return client.Id;
    }

    private static ScheduleSessionRequest Request(
        Guid trainerId, Guid roomId, TimeSlot? slot = null, SessionType type = SessionType.Group, int capacity = 10, string title = "Morning yoga")
    {
        var s = slot ?? TestData.Slot();
        return new()
        {
            Title = title, Type = type, TrainerId = trainerId, RoomId = roomId, Start = s.Start, End = s.End, Capacity = capacity,
        };
    }

    private async Task<SessionResponse> ScheduleAsync(TimeSlot? slot = null, int capacity = 10, Trainer? trainer = null)
    {
        trainer ??= await AddTrainerAsync();
        var room = await AddRoomAsync();
        return await _service.ScheduleAsync(Request(trainer.Id, room.Id, slot, capacity: capacity), Ct);
    }

    [Fact]
    public async Task ScheduleAsync_saves_session_and_returns_it()
    {
        var trainer = await AddTrainerAsync();
        var room = await AddRoomAsync();
        var slot = TestData.Slot();

        var created = await _service.ScheduleAsync(Request(trainer.Id, room.Id, slot, title: " Morning yoga "), Ct);

        Assert.Equal("Morning yoga", created.Title);
        Assert.Equal(SessionType.Group, created.Type);
        Assert.Equal(trainer.Id, created.TrainerId);
        Assert.Equal(room.Id, created.RoomId);
        Assert.Equal(slot.Start, created.Start);
        Assert.Equal(slot.End, created.End);
        Assert.Equal(SessionStatus.Scheduled, created.Status);
        Assert.Empty(created.Bookings);
        Assert.Equal(created.Id, (await _service.GetAsync(created.Id, Ct)).Id);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ScheduleAsync_for_missing_trainer_throws_not_found_and_does_not_save()
    {
        var room = await AddRoomAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ScheduleAsync(Request(Guid.NewGuid(), room.Id), Ct));
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ScheduleAsync_for_missing_room_throws_not_found_and_does_not_save()
    {
        var trainer = await AddTrainerAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ScheduleAsync(Request(trainer.Id, Guid.NewGuid()), Ct));
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ScheduleAsync_with_end_before_start_throws_domain_exception()
    {
        var slot = TestData.Slot();
        var request = Request((await AddTrainerAsync()).Id, (await AddRoomAsync()).Id) with { Start = slot.End, End = slot.Start };

        await Assert.ThrowsAsync<DomainException>(() => _service.ScheduleAsync(request, Ct));
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ScheduleAsync_reads_now_from_the_clock()
    {
        var slot = TestData.Slot();
        _clock.Now = slot.Start.AddMinutes(1);

        var request = Request((await AddTrainerAsync()).Id, (await AddRoomAsync()).Id, slot);

        await Assert.ThrowsAsync<DomainException>(() => _service.ScheduleAsync(request, Ct));
    }

    [Fact]
    public async Task ScheduleAsync_when_trainer_is_busy_throws_domain_exception_and_does_not_save()
    {
        var trainer = await AddTrainerAsync();
        await ScheduleAsync(TestData.Slot(startHour: 10), trainer: trainer);

        await Assert.ThrowsAsync<DomainException>(() => ScheduleAsync(TestData.Slot(startHour: 10, durationMinutes: 30), trainer: trainer));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetAsync_for_missing_session_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ListAsync_returns_sessions_starting_in_range_ordered_by_start()
    {
        var late = await ScheduleAsync(TestData.Slot(startHour: 15));
        var early = await ScheduleAsync(TestData.Slot(startHour: 9));
        await ScheduleAsync(TestData.Slot(daysFromToday: 3));
        var dayStart = TestData.Slot().Start.Date;
        var from = new DateTimeOffset(dayStart, TimeSpan.Zero);

        var list = await _service.ListAsync(from, from.AddDays(1), Ct);

        Assert.Equal([early.Id, late.Id], list.Select(s => s.Id));
    }

    [Fact]
    public async Task ListForTrainerAsync_returns_only_the_linked_trainers_sessions()
    {
        var trainer = await AddTrainerAsync();
        trainer.LinkIdentity("auth0|trainer");
        var own = await ScheduleAsync(trainer: trainer);
        await ScheduleAsync();

        var list = await _service.ListForTrainerAsync("auth0|trainer", TestData.Now, TestData.Now.AddDays(7), Ct);

        Assert.Equal([own.Id], list.Select(s => s.Id));
    }

    [Theory]
    [InlineData("auth0|nobody")]
    [InlineData("")]
    public async Task ListForTrainerAsync_without_linked_trainer_throws_not_found(string identityUserId)
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ListForTrainerAsync(identityUserId, TestData.Now, TestData.Now.AddDays(7), Ct));
    }

    [Fact]
    public async Task BookAsync_adds_booking_at_clock_time_and_saves()
    {
        var session = await ScheduleAsync();
        var clientId = await AddClientWithMembershipAsync();

        var booking = await _service.BookAsync(session.Id, new BookSessionRequest { ClientId = clientId }, Ct);

        Assert.Equal(new BookingResponse(clientId, TestData.Now, null), booking);
        var loaded = await _service.GetAsync(session.Id, Ct);
        Assert.Equal(1, loaded.ActiveBookingCount);
        Assert.Equal([booking], loaded.Bookings);
        Assert.Equal(2, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task BookAsync_for_missing_client_throws_not_found_and_does_not_save()
    {
        var session = await ScheduleAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.BookAsync(session.Id, new BookSessionRequest { ClientId = Guid.NewGuid() }, Ct));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task BookAsync_for_missing_session_throws_not_found()
    {
        var clientId = await AddClientWithMembershipAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.BookAsync(Guid.NewGuid(), new BookSessionRequest { ClientId = clientId }, Ct));
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task BookAsync_for_client_without_membership_throws_domain_exception_and_does_not_save()
    {
        var session = await ScheduleAsync();
        var client = TestData.Client();
        Get<IClientRepository>().Add(client);
        await SeedAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => _service.BookAsync(session.Id, new BookSessionRequest { ClientId = client.Id }, Ct));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelBookingAsync_marks_the_booking_cancelled_at_clock_time()
    {
        var session = await ScheduleAsync();
        var clientId = await AddClientWithMembershipAsync();
        await _service.BookAsync(session.Id, new BookSessionRequest { ClientId = clientId }, Ct);
        _clock.Now = TestData.Now.AddHours(1);

        await _service.CancelBookingAsync(session.Id, clientId, Ct);

        var loaded = await _service.GetAsync(session.Id, Ct);
        Assert.Equal(0, loaded.ActiveBookingCount);
        Assert.Equal(TestData.Now.AddHours(1), Assert.Single(loaded.Bookings).CancelledAt);
        Assert.Equal(3, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelBookingAsync_for_client_without_booking_throws_domain_exception()
    {
        var session = await ScheduleAsync();

        await Assert.ThrowsAsync<DomainException>(() => _service.CancelBookingAsync(session.Id, Guid.NewGuid(), Ct));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelAsync_cancels_session_and_its_bookings()
    {
        var session = await ScheduleAsync();
        await _service.BookAsync(session.Id, new BookSessionRequest { ClientId = await AddClientWithMembershipAsync() }, Ct);

        await _service.CancelAsync(session.Id, Ct);

        var loaded = await _service.GetAsync(session.Id, Ct);
        Assert.Equal(SessionStatus.Cancelled, loaded.Status);
        Assert.Equal(0, loaded.ActiveBookingCount);
        Assert.NotNull(Assert.Single(loaded.Bookings).CancelledAt);
        Assert.Equal(3, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelAsync_after_the_session_started_throws_domain_exception()
    {
        var session = await ScheduleAsync();
        _clock.Now = session.Start;

        await Assert.ThrowsAsync<DomainException>(() => _service.CancelAsync(session.Id, Ct));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelAsync_for_missing_session_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CancelAsync(Guid.NewGuid(), Ct));
        Assert.Equal(0, UnitOfWork.SaveCount);
    }
}
