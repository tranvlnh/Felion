using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations;

public partial class AddKickUserSyncOperation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE discord_sync_jobs DROP CONSTRAINT ck_discord_sync_jobs_operation;");
        migrationBuilder.Sql(
            "ALTER TABLE discord_sync_jobs ADD CONSTRAINT ck_discord_sync_jobs_operation "
            + "CHECK (operation IN ('SynchronizeRoles', 'ClearManagedRoles', 'KickUser'));");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE discord_sync_jobs DROP CONSTRAINT ck_discord_sync_jobs_operation;");
        migrationBuilder.Sql(
            "ALTER TABLE discord_sync_jobs ADD CONSTRAINT ck_discord_sync_jobs_operation "
            + "CHECK (operation IN ('SynchronizeRoles', 'ClearManagedRoles'));");
    }
}
