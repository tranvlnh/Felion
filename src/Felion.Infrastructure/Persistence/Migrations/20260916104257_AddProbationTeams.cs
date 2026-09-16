using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProbationTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "probation_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_probation_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "team_mentors",
                columns: table => new
                {
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_mentors", x => new { x.team_id, x.member_id });
                    table.ForeignKey(
                        name: "FK_team_mentors_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_mentors_probation_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "probation_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_mentors_member_id",
                table: "team_mentors",
                column: "member_id");

            migrationBuilder.AddForeignKey(
                name: "FK_probation_candidates_probation_teams_team_id",
                table: "probation_candidates",
                column: "team_id",
                principalTable: "probation_teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_probation_candidates_probation_teams_team_id",
                table: "probation_candidates");

            migrationBuilder.DropTable(
                name: "team_mentors");

            migrationBuilder.DropTable(
                name: "probation_teams");
        }
    }
}
