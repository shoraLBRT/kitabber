using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Preferences.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockedAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blocked_accounts",
                schema: "preferences",
                columns: table => new
                {
                    owner_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blocked_accounts", x => new { x.owner_account_id, x.account_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_blocked_accounts_account_id",
                schema: "preferences",
                table: "blocked_accounts",
                column: "account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blocked_accounts",
                schema: "preferences");
        }
    }
}
