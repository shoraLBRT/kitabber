using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Blogs.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "blogs",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subsite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => new { x.account_id, x.subsite_id });
                    table.ForeignKey(
                        name: "fk_subscriptions_subsites_subsite_id",
                        column: x => x.subsite_id,
                        principalSchema: "blogs",
                        principalTable: "subsites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_subsite",
                schema: "blogs",
                table: "subscriptions",
                column: "subsite_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "blogs");
        }
    }
}
