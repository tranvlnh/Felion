using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordLinkingSyncJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discord_sync_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_sync_jobs", x => x.id);
                    table.CheckConstraint("ck_discord_sync_jobs_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_discord_sync_jobs_operation", "operation IN ('SynchronizeRoles')");
                    table.CheckConstraint("ck_discord_sync_jobs_status", "status IN ('Pending', 'Running', 'Succeeded', 'Failed')");
                    table.CheckConstraint("ck_discord_sync_jobs_subject_type", "subject_type IN ('Member', 'Probation')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_discord_sync_jobs_status_created_at",
                table: "discord_sync_jobs",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_discord_sync_jobs_subject_type_subject_id_operation_status",
                table: "discord_sync_jobs",
                columns: new[] { "subject_type", "subject_id", "operation", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discord_sync_jobs");
        }
    }
}
