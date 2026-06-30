using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MiscOriginalCopyLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginalCopyId",
                table: "CopyRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CopyRequests_OriginalCopyId",
                table: "CopyRequests",
                column: "OriginalCopyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CopyRequests_CopyRequests_OriginalCopyId",
                table: "CopyRequests",
                column: "OriginalCopyId",
                principalTable: "CopyRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CopyRequests_CopyRequests_OriginalCopyId",
                table: "CopyRequests");

            migrationBuilder.DropIndex(
                name: "IX_CopyRequests_OriginalCopyId",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "OriginalCopyId",
                table: "CopyRequests");
        }
    }
}
