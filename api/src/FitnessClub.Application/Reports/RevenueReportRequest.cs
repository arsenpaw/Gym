using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.Reports;

public sealed record RevenueReportRequest
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    [Range(MinYear, MaxYear)]
    public int? Year { get; init; }

    [Range(1, 12)]
    public int? Month { get; init; }
}
