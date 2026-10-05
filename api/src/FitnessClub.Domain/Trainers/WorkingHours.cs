using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Trainers;

public sealed record WorkingHours
{
    public DayOfWeek Day { get; private init; }
    public TimeOnly Start { get; private init; }
    public TimeOnly End { get; private init; }

    private WorkingHours()
    {
    }

    public static WorkingHours Create(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        if (!Enum.IsDefined(day))
            throw new DomainException("Unknown day of week.");

        if (end <= start)
            throw new DomainException("Working hours must end after they start.");

        return new WorkingHours { Day = day, Start = start, End = end };
    }

    public bool Overlaps(WorkingHours other) => Day == other.Day && Start < other.End && other.Start < End;

    public bool Covers(DayOfWeek day, TimeOnly start, TimeOnly end) => Day == day && Start <= start && end <= End;
}
