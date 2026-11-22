using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Comments.Migrations
{
    /// <inheritdoc />
    public partial class CreateComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "comments");

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    author_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    guest_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    source_channel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source_message = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    text = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.CheckConstraint("ck_comments_kind", "(kind = 'Native' AND author_account_id IS NOT NULL AND guest_name IS NULL) OR (kind = 'Guest' AND author_account_id IS NULL AND guest_name IS NOT NULL AND source_channel IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_comments_author_account_id_created_at",
                schema: "comments",
                table: "comments",
                columns: new[] { "author_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_tree",
                schema: "comments",
                table: "comments",
                columns: new[] { "post_id", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_comments_source",
                schema: "comments",
                table: "comments",
                columns: new[] { "source_channel", "source_message" },
                unique: true,
                filter: "source_channel IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comments",
                schema: "comments");
        }
    }
}
