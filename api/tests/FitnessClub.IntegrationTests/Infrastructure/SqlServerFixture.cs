using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace FitnessClub.IntegrationTests.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private static string? serverConnectionString;

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        serverConnectionString = container.GetConnectionString();
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();

    public static string NewDatabaseConnectionString() =>
        new SqlConnectionStringBuilder(
            serverConnectionString ?? throw new InvalidOperationException("The SQL Server test container has not started."))
        {
            InitialCatalog = $"FitnessClub_{Guid.NewGuid():N}",
        }.ConnectionString;
}
