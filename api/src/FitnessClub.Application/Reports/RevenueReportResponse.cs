namespace FitnessClub.Application.Reports;

public sealed record RevenueReportResponse(
    int Year,
    int? Month,
    DateOnly From,
    DateOnly To,
    decimal Total,
    int PaymentCount,
    IReadOnlyList<RevenueBucket> Breakdown);

public sealed record RevenueBucket(DateOnly From, DateOnly To, decimal Total, int PaymentCount);
