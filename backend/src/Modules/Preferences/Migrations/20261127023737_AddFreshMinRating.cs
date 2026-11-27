using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Preferences.Migrations
{
    /// <inheritdoc />
    public partial class AddFreshMinRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "fresh_min_rating",
                schema: "preferences",
                table: "account_preferences",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fresh_min_rating",
                schema: "preferences",
                table: "account_preferences");
        }
    }
}
