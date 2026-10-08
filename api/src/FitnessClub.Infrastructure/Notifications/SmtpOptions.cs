using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "Fitness Club";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
