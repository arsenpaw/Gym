using System.Net.Mail;
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record EmailAddress
{
    public const int MaxLength = 254;

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static EmailAddress Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new DomainException($"Email must be at most {MaxLength} characters.");

        if (!MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed || !address.Host.Contains('.'))
            throw new DomainException("Email is not valid.");

        return new EmailAddress(trimmed.ToLowerInvariant());
    }

    public override string ToString() => Value;
}
