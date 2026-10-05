using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Domain.Training;

public sealed class TrainingSession : AggregateRoot
{
    public const int TitleMaxLength = 100;
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(4);

    private readonly List<Booking> _bookings = [];

    public string Title { get; private set; } = null!;
    public SessionType Type { get; private set; }
    public Guid TrainerId { get; private set; }
    public Guid RoomId { get; private set; }
    public TimeSlot Slot { get; private set; } = null!;
    public int Capacity { get; private set; }
    public SessionStatus Status { get; private set; }
    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    private TrainingSession()
    {
    }

    internal static TrainingSession Create(
        string title, SessionType type, Trainer trainer, Room room, TimeSlot slot, int capacity, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Session title is required.");

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > TitleMaxLength)
            throw new DomainException($"Session title must be at most {TitleMaxLength} characters.");

        if (!Enum.IsDefined(type))
            throw new DomainException("Unknown session type.");

        if (slot.Start <= now)
            throw new DomainException("A session must be scheduled in the future.");

        if (slot.Duration < MinDuration || slot.Duration > MaxDuration)
            throw new DomainException($"A session must last between {MinDuration.TotalMinutes} minutes and {MaxDuration.TotalHours} hours.");

        if (!trainer.IsActive)
            throw new DomainException("An inactive trainer cannot run sessions.");

        if (!room.IsActive)
            throw new DomainException($"Room '{room.Name}' is not in use.");

        if (!trainer.IsWorkingDuring(slot))
            throw new DomainException("The trainer does not work at that time.");

        if (type == SessionType.Individual && capacity != 1)
            throw new DomainException("An individual session has exactly one place.");

        if (capacity < 1 || capacity > room.Capacity)
            throw new DomainException($"Capacity must be between 1 and the room capacity of {room.Capacity}.");

        return new TrainingSession
        {
            Title = trimmedTitle,
            Type = type,
            TrainerId = trainer.Id,
            RoomId = room.Id,
            Slot = slot,
            Capacity = capacity,
            Status = SessionStatus.Scheduled,
        };
    }

    public int ActiveBookingCount => _bookings.Count(b => b.IsActive);

    internal Booking Book(Client client, DateTimeOffset now)
    {
        EnsureOpen(now);

        if (!client.HasActiveMembershipOn(Slot.Start.ToDateOnly()))
            throw new DomainException("The client has no active membership on the session date.");

        if (_bookings.Any(b => b.IsActive && b.ClientId == client.Id))
            throw new DomainException("The client is already booked for this session.");

        if (ActiveBookingCount >= Capacity)
            throw new DomainException("The session is full.");

        var booking = Booking.Create(client.Id, now);
        _bookings.Add(booking);
        return booking;
    }

    public void CancelBooking(Guid clientId, DateTimeOffset now)
    {
        EnsureOpen(now);

        var booking = _bookings.FirstOrDefault(b => b.IsActive && b.ClientId == clientId)
            ?? throw new DomainException("The client has no booking for this session.");

        booking.Cancel(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureOpen(now);
        foreach (var booking in _bookings.Where(b => b.IsActive))
            booking.Cancel(now);

        Status = SessionStatus.Cancelled;
    }

    private void EnsureOpen(DateTimeOffset now)
    {
        if (Status == SessionStatus.Cancelled)
            throw new DomainException("The session is cancelled.");

        if (now >= Slot.Start)
            throw new DomainException("The session has already started.");
    }
}
