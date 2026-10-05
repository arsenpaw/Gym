using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Rooms;

public sealed class Room : AggregateRoot
{
    public const int NameMaxLength = 100;
    public const int MaxCapacity = 500;

    public string Name { get; private set; } = null!;
    public int Capacity { get; private set; }
    public bool IsActive { get; private set; }

    private Room()
    {
    }

    public static Room Create(string name, int capacity)
    {
        var room = new Room { IsActive = true };
        room.Update(name, capacity);
        return room;
    }

    public void Update(string name, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Room name is required.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new DomainException($"Room name must be at most {NameMaxLength} characters.");

        if (capacity is < 1 or > MaxCapacity)
            throw new DomainException($"Room capacity must be between 1 and {MaxCapacity}.");

        Name = trimmedName;
        Capacity = capacity;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
