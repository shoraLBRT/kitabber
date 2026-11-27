using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Blogs.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "onboarded_at",
                schema: "blogs",
                table: "subsites",
                type: "timestamp with time zone",
                nullable: true);

            // The blogs that existed before the onboarding count as onboarded (ADR-0102): only new accounts see it.
            migrationBuilder.Sql(
                "UPDATE blogs.subsites SET onboarded_at = created_at WHERE type = 'Personal';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "onboarded_at",
                schema: "blogs",
                table: "subsites");
        }
    }
}
