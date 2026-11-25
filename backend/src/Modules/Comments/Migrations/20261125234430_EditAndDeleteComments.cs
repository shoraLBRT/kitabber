using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Comments.Migrations
{
    /// <inheritdoc />
    public partial class EditAndDeleteComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "comments",
                table: "comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "edited_at",
                schema: "comments",
                table: "comments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "comments",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "edited_at",
                schema: "comments",
                table: "comments");
        }
    }
}
