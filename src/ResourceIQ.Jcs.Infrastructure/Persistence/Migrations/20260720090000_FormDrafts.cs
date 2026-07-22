using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FormDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FormKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CopyRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSyncedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDrafts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormDrafts_CopyRequestId",
                table: "FormDrafts",
                column: "CopyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDrafts_UpdatedUtc",
                table: "FormDrafts",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FormDrafts_UserId_FormKey",
                table: "FormDrafts",
                columns: new[] { "UserId", "FormKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormDrafts");
        }
    }
}
