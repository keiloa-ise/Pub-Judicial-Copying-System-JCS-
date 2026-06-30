using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionsAndParagraphFormLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FormTemplateId",
                table: "ParagraphTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectionsJson",
                table: "CopyContents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ParagraphTemplates_FormTemplateId",
                table: "ParagraphTemplates",
                column: "FormTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParagraphTemplates_FormTemplates_FormTemplateId",
                table: "ParagraphTemplates",
                column: "FormTemplateId",
                principalTable: "FormTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParagraphTemplates_FormTemplates_FormTemplateId",
                table: "ParagraphTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ParagraphTemplates_FormTemplateId",
                table: "ParagraphTemplates");

            migrationBuilder.DropColumn(
                name: "FormTemplateId",
                table: "ParagraphTemplates");

            migrationBuilder.DropColumn(
                name: "SectionsJson",
                table: "CopyContents");
        }
    }
}
