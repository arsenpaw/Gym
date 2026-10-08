using FitnessClub.Domain.Clients;

namespace FitnessClub.Application.Notifications;

public sealed record ExpiryReminderEmail(string FirstName, string PlanName, DateOnly EndsOn, int DaysLeft)
{
    public static ExpiryReminderEmail For(Client client, Membership membership, DateOnly today) =>
        new(client.Name.FirstName, membership.PlanName, membership.EndsOn, membership.EndsOn.DayNumber - today.DayNumber);
}
