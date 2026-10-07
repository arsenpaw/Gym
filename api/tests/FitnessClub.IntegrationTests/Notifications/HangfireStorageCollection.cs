namespace FitnessClub.IntegrationTests.Notifications;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HangfireStorageCollection
{
    public const string Name = "Hangfire storage";
}
