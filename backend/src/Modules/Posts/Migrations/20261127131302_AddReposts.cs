using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Posts.Migrations
{
    /// <inheritdoc />
    public partial class AddReposts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "repost_of_post_id",
                schema: "posts",
                table: "posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "repost_text",
                schema: "posts",
                table: "posts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_posts_repost_of_post_id_published_at",
                schema: "posts",
                table: "posts",
                columns: new[] { "repost_of_post_id", "published_at" },
                filter: "repost_of_post_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_posts_repost",
                schema: "posts",
                table: "posts",
                columns: new[] { "author_account_id", "repost_of_post_id" },
                unique: true,
                filter: "repost_of_post_id IS NOT NULL AND deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_posts_repost_of_post_id_published_at",
                schema: "posts",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "ux_posts_repost",
                schema: "posts",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "repost_of_post_id",
                schema: "posts",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "repost_text",
                schema: "posts",
                table: "posts");
        }
    }
}
