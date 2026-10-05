using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Training;

public sealed class Booking : Entity
{
    public Guid ClientId { get; private set; }
    public DateTimeOffset BookedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private Booking()
    {
    }

    internal static Booking Create(Guid clientId, DateTimeOffset bookedAt) =>
        new() { ClientId = clientId, BookedAt = bookedAt };

    public bool IsActive => CancelledAt is null;

    internal void Cancel(DateTimeOffset now) => CancelledAt = now;
}
