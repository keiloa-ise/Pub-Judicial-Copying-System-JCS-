using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopyNumberCourtCodeYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CourtCopyCounters",
                table: "CourtCopyCounters");

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "CourtCopyCounters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourtCopyCounters",
                table: "CourtCopyCounters",
                columns: new[] { "CourtId", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CourtCopyCounters",
                table: "CourtCopyCounters");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "CourtCopyCounters");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourtCopyCounters",
                table: "CourtCopyCounters",
                column: "CourtId");
        }
    }
}
