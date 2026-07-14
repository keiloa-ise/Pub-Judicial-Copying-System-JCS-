using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrintPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrintedById",
                table: "CopyRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PrintedUtc",
                table: "CopyRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_CourtId_State",
                table: "CopyRequests",
                columns: new[] { "CourtId", "State" },
                filter: "[PrintedUtc] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Urgency", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_CourtId_State",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "PrintedById",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "PrintedUtc",
                table: "CopyRequests");
        }
    }
}
