using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Blogs.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rules",
                schema: "blogs",
                table: "subsites",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "topic_moderators",
                schema: "blogs",
                columns: table => new
                {
                    subsite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic_moderators", x => new { x.subsite_id, x.account_id });
                    table.ForeignKey(
                        name: "fk_topic_moderators_subsites_subsite_id",
                        column: x => x.subsite_id,
                        principalSchema: "blogs",
                        principalTable: "subsites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topic_transfer_offers",
                schema: "blogs",
                columns: table => new
                {
                    subsite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic_transfer_offers", x => x.subsite_id);
                    table.ForeignKey(
                        name: "fk_topic_transfer_offers_subsites_subsite_id",
                        column: x => x.subsite_id,
                        principalSchema: "blogs",
                        principalTable: "subsites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_topic_moderators_account_id",
                schema: "blogs",
                table: "topic_moderators",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_topic_transfer_offers_to_account_id",
                schema: "blogs",
                table: "topic_transfer_offers",
                column: "to_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "topic_moderators",
                schema: "blogs");

            migrationBuilder.DropTable(
                name: "topic_transfer_offers",
                schema: "blogs");

            migrationBuilder.DropColumn(
                name: "rules",
                schema: "blogs",
                table: "subsites");
        }
    }
}
