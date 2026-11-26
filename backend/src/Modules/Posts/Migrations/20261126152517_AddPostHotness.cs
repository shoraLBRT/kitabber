using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Posts.Migrations
{
    /// <inheritdoc />
    public partial class AddPostHotness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hotness_formula",
                schema: "posts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    formula = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hotness_formula", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "post_hotness",
                schema: "posts",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comments = table.Column<int>(type: "integer", nullable: false),
                    views = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    formula = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_hotness", x => x.post_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_post_hotness_popular_feed",
                schema: "posts",
                table: "post_hotness",
                columns: new[] { "score", "post_id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hotness_formula",
                schema: "posts");

            migrationBuilder.DropTable(
                name: "post_hotness",
                schema: "posts");
        }
    }
}
