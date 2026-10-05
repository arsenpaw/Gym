using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record PhoneNumber
{
    public const int MinDigits = 10;
    public const int MaxDigits = 15;
    public const int MaxLength = MaxDigits + 1;

    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Phone number is required.");

        var trimmed = value.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var body = hasPlus ? trimmed[1..] : trimmed;

        if (body.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')')))
            throw new DomainException("Phone number can contain only digits, spaces, dashes, parentheses and a leading plus.");

        var digits = new string(body.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < MinDigits or > MaxDigits)
            throw new DomainException($"Phone number must have {MinDigits} to {MaxDigits} digits.");

        return new PhoneNumber(hasPlus ? $"+{digits}" : digits);
    }

    public override string ToString() => Value;
}
