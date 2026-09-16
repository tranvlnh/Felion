using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordRoleMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discord_role_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    discord_role_id = table.Column<long>(type: "bigint", nullable: false),
                    role_name_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_role_mappings", x => x.id);
                    table.CheckConstraint("ck_discord_role_mappings_kind", "kind IN ('Position', 'Probation', 'Department', 'Generation', 'ProbationTeam')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_discord_role_mappings_kind_subject_key",
                table: "discord_role_mappings",
                columns: new[] { "kind", "subject_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discord_role_mappings");
        }
    }
}
