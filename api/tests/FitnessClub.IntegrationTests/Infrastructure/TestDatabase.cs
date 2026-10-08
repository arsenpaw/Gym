using Microsoft.Data.SqlClient;

namespace FitnessClub.IntegrationTests.Infrastructure;

internal static class TestDatabase
{
    private const string DeleteAllRowsSql = """
        DECLARE @tables TABLE (Name nvarchar(300));
        INSERT INTO @tables
        SELECT QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'dbo' AND t.name <> N'__EFMigrationsHistory';

        DECLARE @sql nvarchar(max) = N'';
        SELECT @sql += N'ALTER TABLE ' + Name + N' NOCHECK CONSTRAINT ALL;' FROM @tables;
        SELECT @sql += N'DELETE FROM ' + Name + N';' FROM @tables;
        SELECT @sql += N'ALTER TABLE ' + Name + N' WITH CHECK CHECK CONSTRAINT ALL;' FROM @tables;
        EXEC sp_executesql @sql;
        """;

    public static void DeleteAllRows(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(DeleteAllRowsSql, connection);
        command.ExecuteNonQuery();
    }
}
