using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Comments.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentsDayIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_comments_day",
                schema: "comments",
                table: "comments",
                column: "created_at",
                filter: "kind = 'Native' AND deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_comments_day",
                schema: "comments",
                table: "comments");
        }
    }
}
