using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewer_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_student_id_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewer_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_student_id_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_submissions", x => x.id);
                    table.CheckConstraint("ck_evaluation_submissions_reviewer", "(reviewer_type = 'Peer' AND reviewer_candidate_id IS NOT NULL AND reviewer_member_id IS NULL) OR (reviewer_type = 'Mentor' AND reviewer_member_id IS NOT NULL AND reviewer_candidate_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_evaluation_submissions_evaluation_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "evaluation_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluation_submissions_evaluation_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "evaluation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluation_submissions_members_reviewer_member_id",
                        column: x => x.reviewer_member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_evaluation_submissions_probation_candidates_reviewer_candid~",
                        column: x => x.reviewer_candidate_id,
                        principalTable: "probation_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_evaluation_submissions_probation_candidates_target_candidat~",
                        column: x => x.target_candidate_id,
                        principalTable: "probation_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_prompt_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    question_type_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    text_value = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_answers", x => x.id);
                    table.CheckConstraint("ck_evaluation_answers_value", "(question_type_snapshot = 'Score' AND text_value IS NULL) OR (question_type_snapshot = 'Text' AND score_value IS NULL)");
                    table.ForeignKey(
                        name: "FK_evaluation_answers_evaluation_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "evaluation_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_evaluation_answers_evaluation_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "evaluation_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_answers_question_id",
                table: "evaluation_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_answers_submission_id",
                table: "evaluation_answers",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_submissions_form_id_reviewer_candidate_id_target~",
                table: "evaluation_submissions",
                columns: new[] { "form_id", "reviewer_candidate_id", "target_candidate_id" },
                unique: true,
                filter: "reviewer_candidate_id IS NOT NULL AND target_candidate_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_submissions_form_id_reviewer_member_id_target_ca~",
                table: "evaluation_submissions",
                columns: new[] { "form_id", "reviewer_member_id", "target_candidate_id" },
                unique: true,
                filter: "reviewer_member_id IS NOT NULL AND target_candidate_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_submissions_period_id_form_id",
                table: "evaluation_submissions",
                columns: new[] { "period_id", "form_id" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_submissions_reviewer_candidate_id",
                table: "evaluation_submissions",
                column: "reviewer_candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_submissions_reviewer_member_id",
                table: "evaluation_submissions",
                column: "reviewer_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_submissions_target_candidate_id",
                table: "evaluation_submissions",
                column: "target_candidate_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_answers");

            migrationBuilder.DropTable(
                name: "evaluation_submissions");
        }
    }
}
