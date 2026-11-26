using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Platform.Modules.Reactions.Migrations
{
    /// <inheritdoc />
    public partial class AddReactionSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reaction_set",
                schema: "reactions",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reaction_set", x => x.code);
                });

            migrationBuilder.InsertData(
                schema: "reactions",
                table: "reaction_set",
                columns: new[] { "code", "enabled", "position", "title" },
                values: new object[,]
                {
                    { "clap", true, 4, "Браво" },
                    { "heart", true, 2, "Тепло" },
                    { "idea", true, 5, "Полезно" },
                    { "like", true, 1, "Нравится" },
                    { "sad", true, 7, "Грустно" },
                    { "smile", true, 3, "Улыбка" },
                    { "wow", true, 6, "Удивительно" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reaction_set",
                schema: "reactions");
        }
    }
}
