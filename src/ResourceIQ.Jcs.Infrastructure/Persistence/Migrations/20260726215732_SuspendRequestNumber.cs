using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SuspendRequestNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuspendRequestNumber",
                table: "CopyRequests",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuspendRequestNumber",
                table: "CopyRequests");
        }
    }
}
