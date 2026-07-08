using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseBaseYearUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CourtId_CaseBaseNumber",
                table: "CopyRequests");

            migrationBuilder.AddColumn<int>(
                name: "ReservationYear",
                table: "CopyRequests",
                type: "int",
                nullable: false,
                computedColumnSql: "DATEPART(year, [ReservationDate])",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CourtId_CaseBaseNumber_ReservationYear",
                table: "CopyRequests",
                columns: new[] { "CourtId", "CaseBaseNumber", "ReservationYear" },
                unique: true,
                filter: "[Category] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CourtId_CaseBaseNumber_ReservationYear",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "ReservationYear",
                table: "CopyRequests");

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CourtId_CaseBaseNumber",
                table: "CopyRequests",
                columns: new[] { "CourtId", "CaseBaseNumber" },
                unique: true,
                filter: "[Category] = 1");
        }
    }
}
