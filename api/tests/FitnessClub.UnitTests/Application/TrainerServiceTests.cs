using FitnessClub.Application.Common;
using FitnessClub.Application.Trainers;
using FitnessClub.Domain.Common;
using FitnessClub.UnitTests.Domain;
using FitnessClub.UnitTests.Fakes;

namespace FitnessClub.UnitTests.Application;

public class TrainerServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 30, 0, TimeSpan.FromHours(3));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryClientRepository _clients = new();
    private readonly TrainerService _service;

    public TrainerServiceTests()
    {
        _service = new TrainerService(new InMemoryTrainerRepository(), _clients, _unitOfWork, new FakeTimeProvider(Now));
    }

    private static TrainerRequest Request(
        string firstName = "Taras",
        string lastName = "Bondar",
        string phone = "+380501112233",
        string? email = null,
        string specialization = "Yoga") =>
        new() { FirstName = firstName, LastName = lastName, Phone = phone, Email = email, Specialization = specialization };

    private static WorkingHoursRequest Hours(DayOfWeek day, int startHour, int endHour) =>
        new() { Day = day, Start = new TimeOnly(startHour, 0), End = new TimeOnly(endHour, 0) };

    [Fact]
    public async Task HireAsync_returns_saved_active_trainer()
    {
        var hired = await _service.HireAsync(Request(email: "Taras@Club.com", phone: "+38 (050) 111-22-33"), Ct);

        var loaded = await _service.GetAsync(hired.Id, Ct);
        Assert.Equivalent(hired, loaded, strict: true);
        Assert.True(loaded.IsActive);
        Assert.Equal("+380501112233", loaded.Phone);
        Assert.Equal("taras@club.com", loaded.Email);
        Assert.Equal("Bondar Taras", loaded.FullName);
        Assert.Empty(loaded.WorkingHours);
        Assert.Empty(loaded.Clients);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task HireAsync_with_blank_email_stores_no_email()
    {
        var hired = await _service.HireAsync(Request(email: "  "), Ct);

        Assert.Null(hired.Email);
    }

    [Fact]
    public async Task HireAsync_with_phone_of_another_trainer_throws_conflict_and_does_not_save()
    {
        await _service.HireAsync(Request(phone: "+380501112233"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.HireAsync(Request(firstName: "Ivan", phone: "+380 50 111 22 33"), Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task HireAsync_with_invalid_domain_values_throws_domain_exception()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.HireAsync(Request(phone: "12345"), Ct));
        await Assert.ThrowsAsync<DomainException>(() => _service.HireAsync(Request(email: "not-an-email"), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetAsync_for_missing_trainer_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task UpdateProfileAsync_keeping_own_phone_succeeds()
    {
        var hired = await _service.HireAsync(Request(), Ct);

        var updated = await _service.UpdateProfileAsync(hired.Id, Request(specialization: "  Pilates "), Ct);

        Assert.Equal("Pilates", updated.Specialization);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateProfileAsync_to_another_trainers_phone_throws_conflict()
    {
        await _service.HireAsync(Request(phone: "+380501112233"), Ct);
        var other = await _service.HireAsync(Request(firstName: "Ivan", phone: "+380509998877"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateProfileAsync(other.Id, Request(phone: "+380501112233"), Ct));

        Assert.Equal("+380509998877", (await _service.GetAsync(other.Id, Ct)).Phone);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateProfileAsync_for_missing_trainer_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateProfileAsync(Guid.NewGuid(), Request(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ListAsync_hides_inactive_trainers_unless_requested()
    {
        var bondar = await _service.HireAsync(Request(lastName: "Bondar", phone: "+380501112233"), Ct);
        await _service.HireAsync(Request(lastName: "Antonenko", phone: "+380509998877"), Ct);
        await _service.DeactivateAsync(bondar.Id, Ct);

        var activeOnly = await _service.ListAsync(includeInactive: false, Ct);
        var all = await _service.ListAsync(includeInactive: true, Ct);

        Assert.Equal(["Antonenko"], activeOnly.Select(t => t.LastName));
        Assert.Equal(["Antonenko", "Bondar"], all.Select(t => t.LastName));
    }

    [Fact]
    public async Task ActivateAsync_reactivates_trainer()
    {
        var trainer = await _service.HireAsync(Request(), Ct);
        await _service.DeactivateAsync(trainer.Id, Ct);

        await _service.ActivateAsync(trainer.Id, Ct);

        Assert.True((await _service.GetAsync(trainer.Id, Ct)).IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_for_missing_trainer_throws_not_found_and_does_not_save()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeactivateAsync(Guid.NewGuid(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SetWorkingHoursAsync_replaces_hours_sorted_by_day_and_start()
    {
        var trainer = await _service.HireAsync(Request(), Ct);
        await _service.SetWorkingHoursAsync(trainer.Id, [Hours(DayOfWeek.Friday, 8, 12)], Ct);

        var updated = await _service.SetWorkingHoursAsync(
            trainer.Id, [Hours(DayOfWeek.Tuesday, 14, 20), Hours(DayOfWeek.Monday, 9, 13), Hours(DayOfWeek.Monday, 15, 19)], Ct);

        Assert.Equal(
            [new WorkingHoursResponse(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
                new WorkingHoursResponse(DayOfWeek.Monday, new TimeOnly(15, 0), new TimeOnly(19, 0)),
                new WorkingHoursResponse(DayOfWeek.Tuesday, new TimeOnly(14, 0), new TimeOnly(20, 0))],
            updated.WorkingHours);
        Assert.Equal(3, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SetWorkingHoursAsync_with_empty_list_clears_hours()
    {
        var trainer = await _service.HireAsync(Request(), Ct);
        await _service.SetWorkingHoursAsync(trainer.Id, [Hours(DayOfWeek.Friday, 8, 12)], Ct);

        var updated = await _service.SetWorkingHoursAsync(trainer.Id, [], Ct);

        Assert.Empty(updated.WorkingHours);
    }

    [Fact]
    public async Task SetWorkingHoursAsync_with_overlapping_hours_throws_domain_exception_and_does_not_save()
    {
        var trainer = await _service.HireAsync(Request(), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.SetWorkingHoursAsync(
            trainer.Id, [Hours(DayOfWeek.Monday, 9, 13), Hours(DayOfWeek.Monday, 12, 18)], Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SetWorkingHoursAsync_ending_before_start_throws_domain_exception()
    {
        var trainer = await _service.HireAsync(Request(), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.SetWorkingHoursAsync(trainer.Id, [Hours(DayOfWeek.Monday, 18, 9)], Ct));
    }

    [Fact]
    public async Task AssignClientAsync_adds_client_with_name_and_club_time()
    {
        var trainer = await _service.HireAsync(Request(), Ct);
        var client = TestData.Client();
        _clients.Add(client);

        await _service.AssignClientAsync(trainer.Id, client.Id, Ct);

        var loaded = await _service.GetAsync(trainer.Id, Ct);
        var assigned = Assert.Single(loaded.Clients);
        Assert.Equal(new TrainerClientResponse(client.Id, "Shevchenko Olena", Now), assigned);
        Assert.Equal(TimeSpan.FromHours(3), assigned.AssignedAt.Offset);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AssignClientAsync_for_missing_client_throws_not_found_and_does_not_save()
    {
        var trainer = await _service.HireAsync(Request(), Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.AssignClientAsync(trainer.Id, Guid.NewGuid(), Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AssignClientAsync_for_missing_trainer_throws_not_found()
    {
        var client = TestData.Client();
        _clients.Add(client);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.AssignClientAsync(Guid.NewGuid(), client.Id, Ct));
    }

    [Fact]
    public async Task AssignClientAsync_twice_throws_domain_exception()
    {
        var trainer = await _service.HireAsync(Request(), Ct);
        var client = TestData.Client();
        _clients.Add(client);
        await _service.AssignClientAsync(trainer.Id, client.Id, Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.AssignClientAsync(trainer.Id, client.Id, Ct));
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AssignClientAsync_to_inactive_trainer_throws_domain_exception()
    {
        var trainer = await _service.HireAsync(Request(), Ct);
        await _service.DeactivateAsync(trainer.Id, Ct);
        var client = TestData.Client();
        _clients.Add(client);

        await Assert.ThrowsAsync<DomainException>(() => _service.AssignClientAsync(trainer.Id, client.Id, Ct));
    }

    [Fact]
    public async Task UnassignClientAsync_removes_client()
    {
        var trainer = await _service.HireAsync(Request(), Ct);
        var client = TestData.Client();
        _clients.Add(client);
        await _service.AssignClientAsync(trainer.Id, client.Id, Ct);

        await _service.UnassignClientAsync(trainer.Id, client.Id, Ct);

        Assert.Empty((await _service.GetAsync(trainer.Id, Ct)).Clients);
        Assert.Equal(3, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UnassignClientAsync_for_unassigned_client_throws_domain_exception()
    {
        var trainer = await _service.HireAsync(Request(), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.UnassignClientAsync(trainer.Id, Guid.NewGuid(), Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task LinkIdentityAsync_stores_trimmed_identity()
    {
        var trainer = await _service.HireAsync(Request(), Ct);

        await _service.LinkIdentityAsync(trainer.Id, new LinkIdentityRequest { IdentityUserId = "  auth0|abc " }, Ct);

        Assert.Equal("auth0|abc", (await _service.GetAsync(trainer.Id, Ct)).IdentityUserId);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task LinkIdentityAsync_relinking_same_identity_to_same_trainer_succeeds()
    {
        var trainer = await _service.HireAsync(Request(), Ct);
        await _service.LinkIdentityAsync(trainer.Id, new LinkIdentityRequest { IdentityUserId = "auth0|abc" }, Ct);

        await _service.LinkIdentityAsync(trainer.Id, new LinkIdentityRequest { IdentityUserId = "auth0|abc" }, Ct);

        Assert.Equal(3, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task LinkIdentityAsync_with_identity_of_another_trainer_throws_conflict()
    {
        var first = await _service.HireAsync(Request(phone: "+380501112233"), Ct);
        var second = await _service.HireAsync(Request(firstName: "Ivan", phone: "+380509998877"), Ct);
        await _service.LinkIdentityAsync(first.Id, new LinkIdentityRequest { IdentityUserId = "auth0|abc" }, Ct);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.LinkIdentityAsync(second.Id, new LinkIdentityRequest { IdentityUserId = " auth0|abc" }, Ct));

        Assert.Null((await _service.GetAsync(second.Id, Ct)).IdentityUserId);
        Assert.Equal(3, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task LinkIdentityAsync_for_missing_trainer_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.LinkIdentityAsync(Guid.NewGuid(), new LinkIdentityRequest { IdentityUserId = "auth0|abc" }, Ct));
    }
}
