using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomsAndJudgeRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. New tables (Rooms before JudgeRoom: JudgeRoom FKs reference Rooms) ──
            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JudgeRoom",
                columns: table => new
                {
                    JudgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgeRoom", x => new { x.JudgeId, x.RoomId });
                    table.ForeignKey(
                        name: "FK_JudgeRoom_Judges_JudgeId",
                        column: x => x.JudgeId,
                        principalTable: "Judges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JudgeRoom_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // RoomId added nullable first so existing CopyRequests are backfilled before NOT NULL.
            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "CopyRequests",
                type: "uniqueidentifier",
                nullable: true);

            // ── 2. Data migration (preserve existing data) ──
            // a) One default room (غرفة) per existing court.
            migrationBuilder.Sql(
                "INSERT INTO [Rooms] ([Id], [CourtId], [Code], [Name], [IsActive]) " +
                "SELECT NEWID(), c.[Id], N'R-001', N'الغرفة الأولى', 1 FROM [Courts] c;");

            // b) Move judge↔court assignments to judge↔room (into each court's default room).
            migrationBuilder.Sql(
                "INSERT INTO [JudgeRoom] ([JudgeId], [RoomId]) " +
                "SELECT jc.[JudgeId], r.[Id] FROM [JudgeCourt] jc " +
                "JOIN [Rooms] r ON r.[CourtId] = jc.[CourtId] AND r.[Code] = N'R-001';");

            // c) Backfill any existing copy requests onto their court's default room.
            migrationBuilder.Sql(
                "UPDATE cr SET cr.[RoomId] = r.[Id] FROM [CopyRequests] cr " +
                "JOIN [Rooms] r ON r.[CourtId] = cr.[CourtId] AND r.[Code] = N'R-001';");

            // ── 3. Drop the old join table now that its data has been moved ──
            migrationBuilder.DropTable(name: "JudgeCourt");

            // ── 4. Enforce RoomId NOT NULL + indexes + FK ──
            migrationBuilder.AlterColumn<Guid>(
                name: "RoomId",
                table: "CopyRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_RoomId",
                table: "CopyRequests",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeRoom_RoomId",
                table: "JudgeRoom",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CourtId_Code",
                table: "Rooms",
                columns: new[] { "CourtId", "Code" },
                unique: true);

            // NoAction (not Cascade): CopyRequests already cascades from Courts, and Rooms cascades
            // from Courts — a cascading Room FK would create multiple cascade paths to CopyRequests.
            migrationBuilder.AddForeignKey(
                name: "FK_CopyRequests_Rooms_RoomId",
                table: "CopyRequests",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CopyRequests_Rooms_RoomId",
                table: "CopyRequests");

            migrationBuilder.DropTable(
                name: "JudgeRoom");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_RoomId",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "CopyRequests");

            migrationBuilder.CreateTable(
                name: "JudgeCourt",
                columns: table => new
                {
                    JudgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgeCourt", x => new { x.JudgeId, x.CourtId });
                    table.ForeignKey(
                        name: "FK_JudgeCourt_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JudgeCourt_Judges_JudgeId",
                        column: x => x.JudgeId,
                        principalTable: "Judges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JudgeCourt_CourtId",
                table: "JudgeCourt",
                column: "CourtId");
        }
    }
}
