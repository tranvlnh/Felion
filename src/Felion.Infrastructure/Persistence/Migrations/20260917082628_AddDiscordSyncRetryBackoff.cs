using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordSyncRetryBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_discord_sync_jobs_status_created_at",
                table: "discord_sync_jobs");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "discord_sync_jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateIndex(
                name: "IX_discord_sync_jobs_status_next_attempt_at_created_at",
                table: "discord_sync_jobs",
                columns: new[] { "status", "next_attempt_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_discord_sync_jobs_status_next_attempt_at_created_at",
                table: "discord_sync_jobs");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "discord_sync_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_discord_sync_jobs_status_created_at",
                table: "discord_sync_jobs",
                columns: new[] { "status", "created_at" });
        }
    }
}
