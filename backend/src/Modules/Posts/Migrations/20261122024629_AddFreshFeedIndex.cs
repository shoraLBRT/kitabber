using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Posts.Migrations
{
    /// <inheritdoc />
    public partial class AddFreshFeedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_posts_fresh_feed",
                schema: "posts",
                table: "posts",
                columns: new[] { "published_at", "id" },
                descending: new bool[0],
                filter: "status = 'Published' AND deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_posts_fresh_feed",
                schema: "posts",
                table: "posts");
        }
    }
}
