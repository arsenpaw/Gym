using Microsoft.EntityFrameworkCore.Migrations;

namespace FitnessClub.Infrastructure.Persistence.Migrations;

internal sealed partial class SeedMockData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        new MockData(TimeProvider.System.GetLocalNow()).InsertInto(migrationBuilder);

    protected override void Down(MigrationBuilder migrationBuilder) => MockData.DeleteFrom(migrationBuilder);
}
