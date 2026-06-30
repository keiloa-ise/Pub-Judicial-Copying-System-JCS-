using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerCourtCopyNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CopyNumber",
                table: "CopyRequests");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CourtId",
                table: "CopyRequests");

            migrationBuilder.CreateTable(
                name: "CourtCopyCounters",
                columns: table => new
                {
                    CourtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtCopyCounters", x => x.CourtId);
                    table.ForeignKey(
                        name: "FK_CourtCopyCounters_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CourtId_CopyNumber",
                table: "CopyRequests",
                columns: new[] { "CourtId", "CopyNumber" },
                unique: true,
                filter: "[CopyNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourtCopyCounters");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CourtId_CopyNumber",
                table: "CopyRequests");

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CopyNumber",
                table: "CopyRequests",
                column: "CopyNumber",
                unique: true,
                filter: "[CopyNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CourtId",
                table: "CopyRequests",
                column: "CourtId");
        }
    }
}
