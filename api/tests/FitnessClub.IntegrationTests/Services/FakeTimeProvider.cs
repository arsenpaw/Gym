namespace FitnessClub.IntegrationTests.Services;

internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override TimeZoneInfo LocalTimeZone =>
        TimeZoneInfo.CreateCustomTimeZone("Club", Now.Offset, "Club", "Club");

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public void Advance(TimeSpan by) => Now += by;
}
