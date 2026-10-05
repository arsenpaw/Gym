namespace FitnessClub.Domain.Common;

internal static class DateTimeOffsetExtensions
{
    public static DateOnly ToDateOnly(this DateTimeOffset value) => DateOnly.FromDateTime(value.DateTime);

    public static TimeOnly ToTimeOnly(this DateTimeOffset value) => TimeOnly.FromDateTime(value.DateTime);
}
