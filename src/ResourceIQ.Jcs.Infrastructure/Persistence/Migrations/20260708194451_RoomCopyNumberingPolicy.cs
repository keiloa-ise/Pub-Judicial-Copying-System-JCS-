using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomCopyNumberingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CourtCopyCounters",
                table: "CourtCopyCounters");

            migrationBuilder.AddColumn<int>(
                name: "CopyNumberingPolicy",
                table: "Rooms",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "CourtCopyCounters",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourtCopyCounters",
                table: "CourtCopyCounters",
                columns: new[] { "CourtId", "RoomId", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CourtCopyCounters",
                table: "CourtCopyCounters");

            migrationBuilder.DropColumn(
                name: "CopyNumberingPolicy",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "CourtCopyCounters");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourtCopyCounters",
                table: "CourtCopyCounters",
                columns: new[] { "CourtId", "Year" });
        }
    }
}
