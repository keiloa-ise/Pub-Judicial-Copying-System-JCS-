using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NumberingYearAndFirstBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstBaseNumber",
                table: "CopyRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberingYear",
                table: "CopyRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill existing copies: they were numbered under their reservation year (the old behavior),
            // so set NumberingYear = YEAR(ReservationDate) — keeps their delete/renumber correct.
            migrationBuilder.Sql(
                "UPDATE [CopyRequests] SET [NumberingYear] = YEAR([ReservationDate]) WHERE [NumberingYear] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstBaseNumber",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "NumberingYear",
                table: "CopyRequests");
        }
    }
}
