using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "identity",
                table: "accounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "identity",
                table: "accounts");
        }
    }
}
