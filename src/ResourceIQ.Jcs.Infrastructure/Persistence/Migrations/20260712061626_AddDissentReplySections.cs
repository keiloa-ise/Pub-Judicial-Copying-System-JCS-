using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDissentReplySections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RebuttalSectionsJson",
                table: "CopyContents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RebuttalSectionsJson",
                table: "CopyContents");
        }
    }
}
