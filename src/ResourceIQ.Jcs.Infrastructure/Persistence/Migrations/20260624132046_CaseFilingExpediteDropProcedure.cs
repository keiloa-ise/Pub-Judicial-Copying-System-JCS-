using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseFilingExpediteDropProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JudgeDecisionRef",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "Procedure",
                table: "CopyRequests");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CaseFilingDate",
                table: "CopyRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpediteRequestNumber",
                table: "CopyRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaseFilingDate",
                table: "CopyRequests");

            migrationBuilder.DropColumn(
                name: "ExpediteRequestNumber",
                table: "CopyRequests");

            migrationBuilder.AddColumn<string>(
                name: "JudgeDecisionRef",
                table: "CopyRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Procedure",
                table: "CopyRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
