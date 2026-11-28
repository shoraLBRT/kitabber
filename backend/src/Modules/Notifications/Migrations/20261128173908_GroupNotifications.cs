using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Modules.Notifications.Migrations
{
    /// <inheritdoc />
    public partial class GroupNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "group_key",
                schema: "notifications",
                table: "notifications",
                type: "character varying(96)",
                maxLength: 96,
                nullable: false,
                computedColumnSql: "CASE type WHEN 'Reaction' THEN 'reaction:' || coalesce(post_id::text, '') || ':' || coalesce(comment_id::text, '') WHEN 'Repost' THEN 'repost:' || coalesce(post_id::text, '') WHEN 'PostComment' THEN 'post_comment:' || coalesce(post_id::text, '') WHEN 'NewSubscriber' THEN 'new_subscriber:' || coalesce(blog_id::text, '') ELSE id::text END",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_group",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "recipient_account_id", "group_key", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_group",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "group_key",
                schema: "notifications",
                table: "notifications");
        }
    }
}
