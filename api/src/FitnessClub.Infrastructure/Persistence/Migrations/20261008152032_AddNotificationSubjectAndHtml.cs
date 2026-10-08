using Microsoft.EntityFrameworkCore.Migrations;

namespace FitnessClub.Infrastructure.Persistence.Migrations;

internal sealed partial class AddNotificationSubjectAndHtml : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Notifications_ClientId",
            table: "Notifications");

        migrationBuilder.AddColumn<string>(
            name: "HtmlBody",
            table: "Notifications",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Subject",
            table: "Notifications",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "Your membership expires soon");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_ClientId_CreatedAt",
            table: "Notifications",
            columns: ["ClientId", "CreatedAt"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Notifications_ClientId_CreatedAt",
            table: "Notifications");

        migrationBuilder.DropColumn(
            name: "HtmlBody",
            table: "Notifications");

        migrationBuilder.DropColumn(
            name: "Subject",
            table: "Notifications");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_ClientId",
            table: "Notifications",
            column: "ClientId");
    }
}
