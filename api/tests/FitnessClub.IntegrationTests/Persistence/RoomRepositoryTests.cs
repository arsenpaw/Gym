using FitnessClub.Application.Common;
using FitnessClub.Domain.Rooms;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Persistence;

public class RoomRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    [Fact]
    public async Task Room_round_trips_and_inactive_rooms_are_listed_only_on_request()
    {
        var active = Room.Create($"Room {Guid.NewGuid():N}", 20);
        var inactive = Room.Create($"Room {Guid.NewGuid():N}", 10);
        inactive.Deactivate();
        await SaveAsync<IRoomRepository>(rooms =>
        {
            rooms.Add(active);
            rooms.Add(inactive);
        });

        var activeOnly = await ReadAsync<IRoomRepository, IReadOnlyList<Room>>(rooms => rooms.ListAsync(false, Ct));
        var all = await ReadAsync<IRoomRepository, IReadOnlyList<Room>>(rooms => rooms.ListAsync(true, Ct));

        Assert.Contains(activeOnly, r => r.Id == active.Id && r.Capacity == 20);
        Assert.DoesNotContain(activeOnly, r => r.Id == inactive.Id);
        Assert.Contains(all, r => r.Id == inactive.Id);
        Assert.True(await ReadAsync<IRoomRepository, bool>(rooms => rooms.NameExistsAsync(active.Name.ToLowerInvariant(), null, Ct)));
    }

    [Fact]
    public async Task Saving_a_duplicate_name_past_the_service_check_is_a_conflict_and_writes_nothing()
    {
        var name = $"Room {Guid.NewGuid():N}";
        await SaveAsync<IRoomRepository>(rooms => rooms.Add(Room.Create(name, 20)));

        await Assert.ThrowsAsync<ConflictException>(() => SaveAsync<IRoomRepository>(rooms => rooms.Add(Room.Create(name, 10))));

        var all = await ReadAsync<IRoomRepository, IReadOnlyList<Room>>(rooms => rooms.ListAsync(true, Ct));
        Assert.Single(all, r => r.Name == name);
    }
}
