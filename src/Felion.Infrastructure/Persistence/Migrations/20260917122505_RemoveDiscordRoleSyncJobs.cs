using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDiscordRoleSyncJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM discord_sync_jobs WHERE operation IN ('SynchronizeRoles', 'ClearManagedRoles');");

            migrationBuilder.DropCheckConstraint(
                name: "ck_discord_sync_jobs_operation",
                table: "discord_sync_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_discord_sync_jobs_operation",
                table: "discord_sync_jobs",
                sql: "operation IN ('KickUser')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_discord_sync_jobs_operation",
                table: "discord_sync_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_discord_sync_jobs_operation",
                table: "discord_sync_jobs",
                sql: "operation IN ('SynchronizeRoles', 'ClearManagedRoles', 'KickUser')");
        }
    }
}
