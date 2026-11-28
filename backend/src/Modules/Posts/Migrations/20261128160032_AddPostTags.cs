using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Posts.Migrations
{
    /// <inheritdoc />
    public partial class AddPostTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                schema: "posts",
                table: "posts",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateIndex(
                name: "ix_posts_tags",
                schema: "posts",
                table: "posts",
                column: "tags",
                filter: "status = 'Published' AND deleted_at IS NULL")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_posts_tags",
                schema: "posts",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "tags",
                schema: "posts",
                table: "posts");
        }
    }
}
