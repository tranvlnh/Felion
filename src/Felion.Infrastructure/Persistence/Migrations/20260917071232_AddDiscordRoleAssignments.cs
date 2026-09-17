using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordRoleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discord_role_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discord_role_id = table.Column<long>(type: "bigint", nullable: false),
                    role_name_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_role_assignments", x => x.id);
                    table.CheckConstraint("ck_discord_role_assignments_subject_type", "subject_type IN ('Member', 'Probation')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_discord_role_assignments_discord_role_id",
                table: "discord_role_assignments",
                column: "discord_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_discord_role_assignments_subject_type_subject_id_discord_ro~",
                table: "discord_role_assignments",
                columns: new[] { "subject_type", "subject_id", "discord_role_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discord_role_assignments");
        }
    }
}
