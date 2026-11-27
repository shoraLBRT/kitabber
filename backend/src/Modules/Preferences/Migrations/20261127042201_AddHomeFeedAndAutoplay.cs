using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Preferences.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeFeedAndAutoplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "autoplay_video",
                schema: "preferences",
                table: "account_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true); // existing rows keep the default «on» (ADR-0110)

            migrationBuilder.AddColumn<string>(
                name: "home_feed",
                schema: "preferences",
                table: "account_preferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Popular"); // existing rows keep «Популярное» (ADR-0110)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "autoplay_video",
                schema: "preferences",
                table: "account_preferences");

            migrationBuilder.DropColumn(
                name: "home_feed",
                schema: "preferences",
                table: "account_preferences");
        }
    }
}
