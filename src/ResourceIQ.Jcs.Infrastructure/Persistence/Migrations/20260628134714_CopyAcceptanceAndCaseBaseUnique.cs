using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyAcceptanceAndCaseBaseUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedById",
                table: "CopyRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedUtc",
                table: "CopyRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CourtId_CaseBaseNumber",
                table: "CopyRequests",
                columns: new[] { "CourtId", "CaseBaseNumber" },
                unique: true,
                filter: "[Category] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CourtId_CaseBaseNumber",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "AcceptedById",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "AcceptedUtc",
                table: "CopyRequests");
        }
    }
}
