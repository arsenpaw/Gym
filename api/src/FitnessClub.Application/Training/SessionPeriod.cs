using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.Training;

public sealed record SessionPeriod : IValidatableObject
{
    public const int MaxDays = 92;

    [Required]
    public DateTimeOffset? From { get; init; }

    [Required]
    public DateTimeOffset? To { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is not { } from || To is not { } to)
            yield break;

        if (to <= from)
            yield return new ValidationResult("'to' must be after 'from'.", [nameof(To)]);
        else if (to - from > TimeSpan.FromDays(MaxDays))
            yield return new ValidationResult($"The period can be at most {MaxDays} days.", [nameof(To)]);
    }
}
