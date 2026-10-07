namespace FitnessClub.Application.Reports;

public sealed record DateRangeRequest
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}
