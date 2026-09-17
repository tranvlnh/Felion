using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_by_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_registrations", x => x.id);
                    table.CheckConstraint("ck_event_registrations_status", "status IN ('Pending', 'Approved', 'Rejected', 'Cancelled', 'Assigned')");
                    table.ForeignKey(
                        name: "FK_event_registrations_event_positions_event_position_id",
                        column: x => x.event_position_id,
                        principalTable: "event_positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_registrations_members_assigned_by_member_id",
                        column: x => x.assigned_by_member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_registrations_members_decided_by_member_id",
                        column: x => x.decided_by_member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_registrations_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_registrations_assigned_by_member_id",
                table: "event_registrations",
                column: "assigned_by_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_registrations_decided_by_member_id",
                table: "event_registrations",
                column: "decided_by_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_registrations_event_position_id_member_id",
                table: "event_registrations",
                columns: new[] { "event_position_id", "member_id" },
                unique: true,
                filter: "status IN ('Pending', 'Approved', 'Assigned')");

            migrationBuilder.CreateIndex(
                name: "IX_event_registrations_event_position_id_status",
                table: "event_registrations",
                columns: new[] { "event_position_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_event_registrations_member_id",
                table: "event_registrations",
                column: "member_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_registrations");
        }
    }
}
