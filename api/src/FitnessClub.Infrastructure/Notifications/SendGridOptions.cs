namespace FitnessClub.Infrastructure.Notifications;

internal sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";

    public string? ApiKey { get; set; }

    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "Fitness Club";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
