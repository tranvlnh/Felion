using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Felion.Infrastructure.Persistence;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations;

[DbContext(typeof(FelionDbContext))]
[Migration("20260916110000_AddDiscordClearManagedRolesOperation")]
public partial class AddDiscordClearManagedRolesOperation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_discord_sync_jobs_operation",
            table: "discord_sync_jobs");

        migrationBuilder.AddCheckConstraint(
            name: "ck_discord_sync_jobs_operation",
            table: "discord_sync_jobs",
            sql: "operation IN ('SynchronizeRoles', 'ClearManagedRoles')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_discord_sync_jobs_operation",
            table: "discord_sync_jobs");

        migrationBuilder.AddCheckConstraint(
            name: "ck_discord_sync_jobs_operation",
            table: "discord_sync_jobs",
            sql: "operation IN ('SynchronizeRoles')");
    }
}
