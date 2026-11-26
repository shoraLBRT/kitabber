using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Blogs.Migrations
{
    /// <inheritdoc />
    public partial class AddSubsiteCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cover_media_id",
                schema: "blogs",
                table: "subsites",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cover_position",
                schema: "blogs",
                table: "subsites",
                type: "integer",
                nullable: false,
                // Existing blogs have no cover; the middle is where a new one starts (SubsiteRules.CoverPositionDefault).
                defaultValue: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_media_id",
                schema: "blogs",
                table: "subsites");

            migrationBuilder.DropColumn(
                name: "cover_position",
                schema: "blogs",
                table: "subsites");
        }
    }
}
