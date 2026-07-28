using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyCorrectionStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CopyCorrectionStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CopyRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CopyistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleIndex = table.Column<int>(type: "int", nullable: false),
                    ReturnedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResubmittedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WordsAdded = table.Column<int>(type: "int", nullable: false),
                    WordsRemoved = table.Column<int>(type: "int", nullable: false),
                    TotalWords = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopyCorrectionStats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CopyCorrectionStats_CopyistId",
                table: "CopyCorrectionStats",
                column: "CopyistId");

            migrationBuilder.CreateIndex(
                name: "IX_CopyCorrectionStats_CopyRequestId",
                table: "CopyCorrectionStats",
                column: "CopyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CopyCorrectionStats_ResubmittedUtc",
                table: "CopyCorrectionStats",
                column: "ResubmittedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CopyCorrectionStats");
        }
    }
}
