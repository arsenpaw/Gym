using FitnessClub.Domain.Clients;

namespace FitnessClub.Application.Clients;

public sealed record ClientDetailsResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string FullName,
    DateOnly DateOfBirth,
    int Age,
    string Email,
    string? Phone,
    DateTimeOffset RegisteredAt,
    ActiveMembershipResponse? ActiveMembership,
    IReadOnlyList<MembershipResponse> Memberships)
{
    public static ClientDetailsResponse FromEntity(Client client, DateOnly today) =>
        new(
            client.Id,
            client.Name.FirstName,
            client.Name.LastName,
            client.Name.MiddleName,
            client.Name.FullName,
            client.DateOfBirth,
            client.AgeOn(today),
            client.Email.Value,
            client.Phone?.Value,
            client.RegisteredAt,
            ActiveMembershipResponse.For(client, today),
            client.Memberships
                .OrderByDescending(m => m.StartsOn)
                .ThenByDescending(m => m.PurchasedAt)
                .Select(m => MembershipResponse.FromEntity(m, today))
                .ToList());
}
