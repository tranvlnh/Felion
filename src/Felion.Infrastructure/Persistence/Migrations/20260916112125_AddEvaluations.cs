using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_periods", x => x.id);
                    table.CheckConstraint("ck_evaluation_periods_status", "status IN ('Draft', 'Open', 'Closed')");
                });

            migrationBuilder.CreateTable(
                name: "evaluation_forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reviewer_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_forms", x => x.id);
                    table.CheckConstraint("ck_evaluation_forms_reviewer_type", "reviewer_type IN ('Peer', 'Mentor')");
                    table.ForeignKey(
                        name: "FK_evaluation_forms_evaluation_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "evaluation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    score_min = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    score_max = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    text_max_length = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_questions", x => x.id);
                    table.CheckConstraint("ck_evaluation_questions_configuration", "(type = 'Score' AND score_min IS NOT NULL AND score_max IS NOT NULL AND score_min <= score_max AND text_max_length IS NULL) OR (type = 'Text' AND score_min IS NULL AND score_max IS NULL AND text_max_length IS NOT NULL AND text_max_length > 0)");
                    table.CheckConstraint("ck_evaluation_questions_order", "order_number > 0");
                    table.ForeignKey(
                        name: "FK_evaluation_questions_evaluation_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "evaluation_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_forms_period_id",
                table: "evaluation_forms",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_periods_status",
                table: "evaluation_periods",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_questions_form_id_order_number",
                table: "evaluation_questions",
                columns: new[] { "form_id", "order_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_questions");

            migrationBuilder.DropTable(
                name: "evaluation_forms");

            migrationBuilder.DropTable(
                name: "evaluation_periods");
        }
    }
}
