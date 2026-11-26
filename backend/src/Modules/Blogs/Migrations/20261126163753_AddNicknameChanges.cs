using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Blogs.Migrations
{
    /// <inheritdoc />
    public partial class AddNicknameChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "nickname_changed_at",
                schema: "blogs",
                table: "subsites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "former_nicknames",
                schema: "blogs",
                columns: table => new
                {
                    nickname = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subsite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_former_nicknames", x => x.nickname);
                    table.ForeignKey(
                        name: "fk_former_nicknames_subsites_subsite_id",
                        column: x => x.subsite_id,
                        principalSchema: "blogs",
                        principalTable: "subsites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_former_nicknames_subsite_id",
                schema: "blogs",
                table: "former_nicknames",
                column: "subsite_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "former_nicknames",
                schema: "blogs");

            migrationBuilder.DropColumn(
                name: "nickname_changed_at",
                schema: "blogs",
                table: "subsites");
        }
    }
}
