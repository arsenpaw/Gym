using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record TimeSlot
{
    public DateTimeOffset Start { get; private init; }
    public DateTimeOffset End { get; private init; }

    private TimeSlot()
    {
    }

    public static TimeSlot Create(DateTimeOffset start, DateTimeOffset end) =>
        end <= start
            ? throw new DomainException("A time slot must end after it starts.")
            : new TimeSlot { Start = start, End = end };

    public TimeSpan Duration => End - Start;

    public bool Overlaps(TimeSlot other) => Start < other.End && other.Start < End;
}
