using System.ComponentModel.DataAnnotations;
using FitnessClub.Domain.MembershipPlans;

namespace FitnessClub.Application.MembershipPlans;

public sealed record MembershipPlanRequest
{
    [Required]
    [StringLength(MembershipPlan.NameMaxLength)]
    public string Name { get; init; } = "";

    [Range(0.01, 1_000_000)]
    public decimal Price { get; init; }

    [Range(1, MembershipPlan.MaxValidityDays)]
    public int ValidityDays { get; init; }

    [Range(1, MembershipPlan.MaxVisitLimit)]
    public int? VisitLimit { get; init; }
}
