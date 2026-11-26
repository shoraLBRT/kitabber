using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Posts.Migrations
{
    /// <inheritdoc />
    public partial class AddPostViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "impression_count",
                schema: "posts",
                table: "posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "view_count",
                schema: "posts",
                table: "posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "post_views",
                schema: "posts",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    viewer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_seconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_views", x => new { x.post_id, x.kind, x.viewer, x.period_start });
                });

            migrationBuilder.CreateTable(
                name: "view_salts",
                schema: "posts",
                columns: table => new
                {
                    period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    salt = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_view_salts", x => x.period_start);
                });

            migrationBuilder.CreateIndex(
                name: "ix_post_views_period_start",
                schema: "posts",
                table: "post_views",
                column: "period_start");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_views",
                schema: "posts");

            migrationBuilder.DropTable(
                name: "view_salts",
                schema: "posts");

            migrationBuilder.DropColumn(
                name: "impression_count",
                schema: "posts",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "view_count",
                schema: "posts",
                table: "posts");
        }
    }
}
