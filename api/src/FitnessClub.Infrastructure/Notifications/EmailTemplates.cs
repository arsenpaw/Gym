using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Notifications;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed partial class EmailTemplates : IEmailTemplates
{
    private static readonly string ExpiryReminderHtml = Load("ExpiryReminder.html");
    private static readonly string PromotionHtml = Load("Promotion.html");

    public NotificationContent ExpiryReminder(ExpiryReminderEmail email)
    {
        var endsOn = Format(email.EndsOn);

        return NotificationContent.Create(
            "Your membership expires soon",
            $"Dear {email.FirstName}, your membership '{email.PlanName}' ends on {endsOn}. Renew at the reception desk to keep training without a break.",
            Fill(ExpiryReminderHtml, new Dictionary<string, string>
            {
                ["FirstName"] = email.FirstName,
                ["PlanName"] = email.PlanName,
                ["EndsOn"] = endsOn,
            }));
    }

    public NotificationContent Promotion(PromotionEmail email)
    {
        var validUntil = Format(email.ValidUntil);
        var discount = email.DiscountPercent.ToString(CultureInfo.InvariantCulture);

        return NotificationContent.Create(
            $"{discount}% off your next membership",
            $"Dear {email.FirstName}, this month only: get {discount}% off any membership. Show this email at the reception desk. Valid until {validUntil}.",
            Fill(PromotionHtml, new Dictionary<string, string>
            {
                ["FirstName"] = email.FirstName,
                ["Discount"] = discount,
                ["ValidUntil"] = validUntil,
            }));
    }

    private static string Format(DateOnly date) => date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value)
                ? WebUtility.HtmlEncode(value)
                : throw new InvalidOperationException($"The email template has no value for '{match.Groups[1].Value}'."));

    private static string Load(string name)
    {
        var resource = $"{typeof(EmailTemplates).Namespace}.Templates.{name}";
        using var stream = typeof(EmailTemplates).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"The email template '{resource}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();
}
