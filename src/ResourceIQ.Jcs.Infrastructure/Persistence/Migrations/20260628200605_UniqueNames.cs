using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CourtId_Name",
                table: "Rooms",
                columns: new[] { "CourtId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Judges_Name",
                table: "Judges",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courts_Name",
                table: "Courts",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rooms_CourtId_Name",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Judges_Name",
                table: "Judges");

            migrationBuilder.DropIndex(
                name: "IX_Courts_Name",
                table: "Courts");
        }
    }
}
