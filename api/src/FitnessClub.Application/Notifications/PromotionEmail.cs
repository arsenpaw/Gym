namespace FitnessClub.Application.Notifications;

public sealed record PromotionEmail(string FirstName, int DiscountPercent, DateOnly ValidUntil);
