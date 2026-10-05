using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record PersonName
{
    public const int PartMaxLength = 100;

    public string FirstName { get; private init; } = null!;
    public string LastName { get; private init; } = null!;
    public string? MiddleName { get; private init; }

    private PersonName()
    {
    }

    public static PersonName Create(string firstName, string lastName, string? middleName) =>
        new()
        {
            FirstName = Required(firstName, "First name"),
            LastName = Required(lastName, "Last name"),
            MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : Limited(middleName.Trim(), "Middle name"),
        };

    public string FullName => MiddleName is null ? $"{LastName} {FirstName}" : $"{LastName} {FirstName} {MiddleName}";

    private static string Required(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} is required.");

        return Limited(value.Trim(), field);
    }

    private static string Limited(string value, string field) =>
        value.Length > PartMaxLength
            ? throw new DomainException($"{field} must be at most {PartMaxLength} characters.")
            : value;
}
