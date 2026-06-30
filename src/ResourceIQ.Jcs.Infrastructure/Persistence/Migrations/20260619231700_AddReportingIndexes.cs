using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CopyRequests_Rooms_RoomId",
                table: "CopyRequests");

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_ApprovedById",
                table: "CopyRequests",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_AssignedCopyistId",
                table: "CopyRequests",
                column: "AssignedCopyistId");

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CreatedUtc",
                table: "CopyRequests",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_State",
                table: "CopyRequests",
                column: "State");

            migrationBuilder.AddForeignKey(
                name: "FK_CopyRequests_Rooms_RoomId",
                table: "CopyRequests",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CopyRequests_Rooms_RoomId",
                table: "CopyRequests");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_ApprovedById",
                table: "CopyRequests");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_AssignedCopyistId",
                table: "CopyRequests");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CreatedUtc",
                table: "CopyRequests");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_State",
                table: "CopyRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_CopyRequests_Rooms_RoomId",
                table: "CopyRequests",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
