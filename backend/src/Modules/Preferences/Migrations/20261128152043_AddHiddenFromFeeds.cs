using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Preferences.Migrations
{
    /// <inheritdoc />
    public partial class AddHiddenFromFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hidden_posts",
                schema: "preferences",
                columns: table => new
                {
                    owner_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hidden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hidden_posts", x => new { x.owner_account_id, x.post_id });
                });

            migrationBuilder.CreateTable(
                name: "hidden_topics",
                schema: "preferences",
                columns: table => new
                {
                    owner_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blog_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hidden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hidden_topics", x => new { x.owner_account_id, x.blog_id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hidden_posts",
                schema: "preferences");

            migrationBuilder.DropTable(
                name: "hidden_topics",
                schema: "preferences");
        }
    }
}
