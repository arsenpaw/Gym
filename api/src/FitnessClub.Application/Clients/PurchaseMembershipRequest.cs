using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FitnessClub.Domain.Payments;

namespace FitnessClub.Application.Clients;

public sealed record PurchaseMembershipRequest
{
    [Required]
    public Guid? PlanId { get; init; }

    public DateOnly? StartsOn { get; init; }

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))]
    public PaymentMethod? PaymentMethod { get; init; }
}
