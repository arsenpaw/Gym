using Microsoft.EntityFrameworkCore.Migrations;

namespace FitnessClub.Infrastructure.Persistence.Migrations;

internal sealed partial class ClientEmailRequired : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE Clients SET Email = 'client-' + LOWER(CONVERT(nvarchar(36), Id)) + '@example.com' WHERE Email IS NULL;");

        migrationBuilder.DropIndex(
            name: "IX_Clients_Phone",
            table: "Clients");

        migrationBuilder.AlterColumn<string>(
            name: "Phone",
            table: "Clients",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(16)",
            oldMaxLength: 16);

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Clients",
            type: "nvarchar(254)",
            maxLength: 254,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(254)",
            oldMaxLength: 254,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Clients_Email",
            table: "Clients",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Clients_Email",
            table: "Clients");

        migrationBuilder.Sql(
            """
            WITH missing AS (SELECT Phone, ROW_NUMBER() OVER (ORDER BY Id) AS Number FROM Clients WHERE Phone IS NULL)
            UPDATE missing SET Phone = '+999' + RIGHT('0000000000' + CAST(Number AS varchar(10)), 10);
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Phone",
            table: "Clients",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(16)",
            oldMaxLength: 16,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Clients",
            type: "nvarchar(254)",
            maxLength: 254,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(254)",
            oldMaxLength: 254);

        migrationBuilder.CreateIndex(
            name: "IX_Clients_Phone",
            table: "Clients",
            column: "Phone",
            unique: true);
    }
}
