using FitnessClub.Domain.Common;
using FitnessClub.Domain.Rooms;

namespace FitnessClub.UnitTests.Domain;

public class RoomTests
{
    [Fact]
    public void Create_trims_name_and_starts_active()
    {
        var room = Room.Create("  Yoga hall ", 25);

        Assert.Equal("Yoga hall", room.Name);
        Assert.Equal(25, room.Capacity);
        Assert.True(room.IsActive);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("Hall", 0)]
    [InlineData("Hall", Room.MaxCapacity + 1)]
    public void Create_with_invalid_values_throws(string name, int capacity)
    {
        Assert.Throws<DomainException>(() => Room.Create(name, capacity));
    }

    [Fact]
    public void Deactivate_then_activate_toggles_IsActive()
    {
        var room = Room.Create("Hall", 10);

        room.Deactivate();
        Assert.False(room.IsActive);

        room.Activate();
        Assert.True(room.IsActive);
    }
}
