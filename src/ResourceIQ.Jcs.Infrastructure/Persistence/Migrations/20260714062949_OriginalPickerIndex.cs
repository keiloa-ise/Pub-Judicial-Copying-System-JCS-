using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OriginalPickerIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CourtId_RoomId",
                table: "CopyRequests",
                columns: new[] { "CourtId", "RoomId" },
                filter: "[Category] = 1 AND [State] = 4")
                .Annotation("SqlServer:Include", new[] { "CopyNumber", "CaseBaseNumber", "ReservationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CourtId_RoomId",
                table: "CopyRequests");
        }
    }
}
