using FitnessClub.Application.Common;
using FitnessClub.Application.Rooms;
using FitnessClub.Domain.Common;
using FitnessClub.UnitTests.Fakes;

namespace FitnessClub.UnitTests.Application;

public class RoomServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RoomService _service;

    public RoomServiceTests()
    {
        _service = new RoomService(new InMemoryRoomRepository(), _unitOfWork);
    }

    private static RoomRequest Request(string name = "Hall A", int capacity = 20) =>
        new() { Name = name, Capacity = capacity };

    [Fact]
    public async Task CreateAsync_returns_saved_active_room_with_trimmed_name()
    {
        var created = await _service.CreateAsync(Request("  Hall A  ", 25), Ct);

        var loaded = await _service.GetAsync(created.Id, Ct);
        Assert.Equal(created, loaded);
        Assert.Equal("Hall A", loaded.Name);
        Assert.Equal(25, loaded.Capacity);
        Assert.True(loaded.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_with_duplicate_name_ignoring_case_and_spaces_throws_conflict_and_does_not_save()
    {
        await _service.CreateAsync(Request("Hall A"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Request("  HALL a "), Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("   ", 20)]
    [InlineData("Hall", 0)]
    [InlineData("Hall", 501)]
    public async Task CreateAsync_with_invalid_domain_values_throws_domain_exception(string name, int capacity)
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(Request(name, capacity), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetAsync_for_missing_room_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task UpdateAsync_keeping_own_name_succeeds()
    {
        var created = await _service.CreateAsync(Request("Hall A", 20), Ct);

        var updated = await _service.UpdateAsync(created.Id, Request("hall a", 30), Ct);

        Assert.Equal(created with { Name = "hall a", Capacity = 30 }, updated);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_to_another_rooms_name_throws_conflict()
    {
        await _service.CreateAsync(Request("Hall A"), Ct);
        var hallB = await _service.CreateAsync(Request("Hall B"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(hallB.Id, Request("hall a"), Ct));

        Assert.Equal("Hall B", (await _service.GetAsync(hallB.Id, Ct)).Name);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_with_invalid_capacity_throws_domain_exception_and_keeps_room()
    {
        var created = await _service.CreateAsync(Request("Hall A", 20), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.UpdateAsync(created.Id, Request("Hall A", 0), Ct));

        Assert.Equal(20, (await _service.GetAsync(created.Id, Ct)).Capacity);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_for_missing_room_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(Guid.NewGuid(), Request(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ActivateAsync_for_missing_room_throws_not_found_and_does_not_save()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ActivateAsync(Guid.NewGuid(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeactivateAsync_for_missing_room_throws_not_found_and_does_not_save()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeactivateAsync(Guid.NewGuid(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ListAsync_hides_inactive_rooms_unless_requested()
    {
        var hallA = await _service.CreateAsync(Request("Hall A"), Ct);
        await _service.CreateAsync(Request("Hall B"), Ct);
        await _service.DeactivateAsync(hallA.Id, Ct);

        var activeOnly = await _service.ListAsync(includeInactive: false, Ct);
        var all = await _service.ListAsync(includeInactive: true, Ct);

        Assert.Equal(["Hall B"], activeOnly.Select(r => r.Name));
        Assert.Equal(["Hall A", "Hall B"], all.Select(r => r.Name));
    }

    [Fact]
    public async Task ActivateAsync_reactivates_room()
    {
        var room = await _service.CreateAsync(Request(), Ct);
        await _service.DeactivateAsync(room.Id, Ct);

        await _service.ActivateAsync(room.Id, Ct);

        Assert.True((await _service.GetAsync(room.Id, Ct)).IsActive);
        Assert.Equal(3, _unitOfWork.SaveCount);
    }
}
