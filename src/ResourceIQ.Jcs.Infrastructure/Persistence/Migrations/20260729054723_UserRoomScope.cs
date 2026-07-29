using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserRoomScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserRoom",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoom", x => new { x.UserId, x.RoomId });
                    table.ForeignKey(
                        name: "FK_UserRoom_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoom_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoom_RoomId",
                table: "UserRoom",
                column: "RoomId");

            // Backfill (preserve existing access): every Copyist/Reviewer (Role 3/4) currently scoped to
            // a court is granted every room in that court, so room-level scoping starts with no loss of
            // access. Registry Heads (Role 2) keep their court scope via UserCourt untouched.
            migrationBuilder.Sql(@"
INSERT INTO [UserRoom] ([UserId], [RoomId])
SELECT DISTINCT uc.[UserId], r.[Id]
FROM [UserCourt] uc
INNER JOIN [Users] u ON u.[Id] = uc.[UserId]
INNER JOIN [Rooms] r ON r.[CourtId] = uc.[CourtId]
WHERE u.[Role] IN (3, 4);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRoom");
        }
    }
}
