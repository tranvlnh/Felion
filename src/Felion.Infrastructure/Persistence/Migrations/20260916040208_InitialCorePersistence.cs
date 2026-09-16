using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCorePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_core = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                    table.CheckConstraint("ck_departments_core_slug", "NOT is_core OR slug = 'core'");
                });

            migrationBuilder.CreateTable(
                name: "discord_identity_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    discord_user_id = table.Column<long>(type: "bigint", nullable: false),
                    student_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_identity_links", x => x.id);
                    table.CheckConstraint("ck_discord_identity_links_subject_type", "subject_type IN ('Member', 'Probation')");
                });

            migrationBuilder.CreateTable(
                name: "generations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    club_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_members", x => x.id);
                    table.CheckConstraint("ck_members_position", "position IN ('Admin', 'Core', 'Member')");
                    table.CheckConstraint("ck_members_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "FK_members_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_members_generations_generation_id",
                        column: x => x.generation_id,
                        principalTable: "generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "probation_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_probation_candidates", x => x.id);
                    table.CheckConstraint("ck_probation_candidates_status", "status IN ('Active', 'Passed', 'Failed', 'Archived')");
                    table.ForeignKey(
                        name: "FK_probation_candidates_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_probation_candidates_generations_generation_id",
                        column: x => x.generation_id,
                        principalTable: "generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    actor_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_discord_user_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.CheckConstraint("ck_audit_logs_actor", "(actor_type = 'WebMember' AND actor_member_id IS NOT NULL AND actor_discord_user_id IS NULL) OR (actor_type = 'DiscordMember' AND actor_member_id IS NULL AND actor_discord_user_id IS NOT NULL) OR (actor_type = 'System' AND actor_member_id IS NULL AND actor_discord_user_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_audit_logs_members_actor_member_id",
                        column: x => x.actor_member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "departments",
                columns: new[] { "id", "is_active", "is_core", "name", "slug" },
                values: new object[] { new Guid("4f8d4a6b-6a1d-4b5c-9ef7-8b9b8d1f4b21"), true, true, "Core", "core" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_member_id",
                table: "audit_logs",
                column: "actor_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_correlation_id",
                table: "audit_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_departments_is_core",
                table: "departments",
                column: "is_core",
                unique: true,
                filter: "is_core = true");

            migrationBuilder.CreateIndex(
                name: "IX_departments_slug",
                table: "departments",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_discord_identity_links_discord_user_id",
                table: "discord_identity_links",
                column: "discord_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_discord_identity_links_student_id",
                table: "discord_identity_links",
                column: "student_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_discord_identity_links_subject_id",
                table: "discord_identity_links",
                column: "subject_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_generations_code",
                table: "generations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_members_club_email",
                table: "members",
                column: "club_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_members_department_id",
                table: "members",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_members_generation_id",
                table: "members",
                column: "generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_members_student_id",
                table: "members",
                column: "student_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_probation_candidates_department_id",
                table: "probation_candidates",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_probation_candidates_generation_id",
                table: "probation_candidates",
                column: "generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_probation_candidates_student_id",
                table: "probation_candidates",
                column: "student_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_probation_candidates_team_id",
                table: "probation_candidates",
                column: "team_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "discord_identity_links");

            migrationBuilder.DropTable(
                name: "probation_candidates");

            migrationBuilder.DropTable(
                name: "members");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "generations");
        }
    }
}
