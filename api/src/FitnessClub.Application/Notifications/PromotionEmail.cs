using FitnessClub.Domain.Clients;

namespace FitnessClub.Application.Notifications;

public sealed record PromotionEmail(string FirstName, int DiscountPercent, DateOnly ValidUntil)
{
    public const int CurrentDiscountPercent = 10;

    public static PromotionEmail For(Client client, DateOnly today) =>
        new(client.Name.FirstName, CurrentDiscountPercent, new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)));
}
