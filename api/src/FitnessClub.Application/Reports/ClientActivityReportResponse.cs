namespace FitnessClub.Application.Reports;

public sealed record ClientActivityReportResponse(DateOnly From, DateOnly To, IReadOnlyList<ClientActivityItem> Clients);

public sealed record ClientActivityItem(
    Guid ClientId,
    string FullName,
    int Age,
    string Email,
    string? Phone,
    ActiveMembershipSummary? ActiveMembership,
    int VisitCount,
    DateTimeOffset? LastVisitAt);

public sealed record ActiveMembershipSummary(Guid MembershipId, string PlanName, DateOnly StartsOn, DateOnly EndsOn, int? RemainingVisits);
