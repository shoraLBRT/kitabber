using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Media.Migrations
{
    /// <inheritdoc />
    public partial class AddImagePreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preview_color",
                schema: "media",
                table: "media_files",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "preview_image",
                schema: "media",
                table: "media_files",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preview_color",
                schema: "media",
                table: "media_files");

            migrationBuilder.DropColumn(
                name: "preview_image",
                schema: "media",
                table: "media_files");
        }
    }
}
