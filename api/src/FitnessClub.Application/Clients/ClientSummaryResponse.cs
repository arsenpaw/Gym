using FitnessClub.Domain.Clients;

namespace FitnessClub.Application.Clients;

public sealed record ClientSummaryResponse(
    Guid Id,
    string FullName,
    int Age,
    string Email,
    string? Phone,
    ActiveMembershipResponse? ActiveMembership)
{
    public static ClientSummaryResponse FromEntity(Client client, DateOnly today) =>
        new(
            client.Id,
            client.Name.FullName,
            client.AgeOn(today),
            client.Email.Value,
            client.Phone?.Value,
            ActiveMembershipResponse.For(client, today));
}
