using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Reactions.Migrations
{
    /// <inheritdoc />
    public partial class CreateRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ratings",
                schema: "reactions",
                columns: table => new
                {
                    target_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ratings", x => new { x.target_kind, x.target_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_ratings_post",
                schema: "reactions",
                table: "ratings",
                column: "post_id");

            // The likes put before ratings existed: a like weighs 1 (ReactionWeights, ADR-0081).
            migrationBuilder.Sql("""
                INSERT INTO reactions.ratings (target_kind, target_id, post_id, value)
                SELECT target_kind, target_id, min(post_id::text)::uuid, count(*)
                FROM reactions.reactions
                WHERE kind = 'Like'
                GROUP BY target_kind, target_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ratings",
                schema: "reactions");
        }
    }
}
