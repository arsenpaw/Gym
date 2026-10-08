using Microsoft.Data.SqlClient;

namespace FitnessClub.IntegrationTests.Infrastructure;

public class TestDatabaseTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] DomainTables =
    [
        "MembershipPlans", "Clients", "Memberships", "Visits", "Payments", "Trainers",
        "TrainerWorkingHours", "TrainerClients", "Rooms", "TrainingSessions", "Bookings", "Notifications",
    ];

    [Fact]
    public async Task Factory_database_is_migrated_with_no_rows()
    {
        _ = factory.Services;
        await using var connection = new SqlConnection(factory.ConnectionString);
        await connection.OpenAsync(Ct);

        Assert.True(await CountAsync(connection, "__EFMigrationsHistory") >= 2);
        foreach (var table in DomainTables)
            Assert.Equal(0, await CountAsync(connection, table));
    }

    private static async Task<int> CountAsync(SqlConnection connection, string table)
    {
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{table}]", connection);
        return (int)(await command.ExecuteScalarAsync(Ct))!;
    }
}
