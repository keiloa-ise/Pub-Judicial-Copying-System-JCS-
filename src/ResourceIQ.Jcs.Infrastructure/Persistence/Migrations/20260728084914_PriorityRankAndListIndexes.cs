using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PriorityRankAndListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PriorityRank",
                table: "CopyRequests",
                type: "int",
                nullable: false,
                computedColumnSql: "CASE [Urgency] WHEN 1 THEN 0 WHEN 2 THEN 1 ELSE 2 END",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_PriorityRank_CreatedUtc",
                table: "CopyRequests",
                columns: new[] { "PriorityRank", "CreatedUtc" })
                .Annotation("SqlServer:Include", new[] { "State", "CourtId", "RoomId", "AssignedCopyistId" });

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_ReservationDate",
                table: "CopyRequests",
                column: "ReservationDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_PriorityRank_CreatedUtc",
                table: "CopyRequests");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_ReservationDate",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "PriorityRank",
                table: "CopyRequests");
        }
    }
}
