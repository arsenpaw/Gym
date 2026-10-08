using FitnessClub.Domain.Clients;

namespace FitnessClub.Application.Notifications;

public sealed record ExpiryReminderEmail(string FirstName, string PlanName, DateOnly EndsOn)
{
    public static ExpiryReminderEmail For(Client client, Membership membership) =>
        new(client.Name.FirstName, membership.PlanName, membership.EndsOn);
}
