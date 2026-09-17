using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceConfigurableEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_answers");

            migrationBuilder.DropTable(
                name: "evaluation_questions");

            migrationBuilder.DropTable(
                name: "evaluation_submissions");

            migrationBuilder.DropTable(
                name: "evaluation_forms");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evaluation_periods_status",
                table: "evaluation_periods");

            migrationBuilder.DropColumn(
                name: "ends_at",
                table: "evaluation_periods");

            migrationBuilder.DropColumn(
                name: "starts_at",
                table: "evaluation_periods");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "opened_at",
                table: "evaluation_periods",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                table: "evaluation_periods",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mentor_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentor_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentor_student_id_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mentor_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_student_id_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attendance = table.Column<int>(type: "integer", nullable: false),
                    task_completion = table.Column<int>(type: "integer", nullable: false),
                    learning_initiative = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentor_evaluations", x => x.id);
                    table.CheckConstraint("ck_mentor_evaluations_scores", "attendance BETWEEN 1 AND 10 AND task_completion BETWEEN 1 AND 10 AND learning_initiative BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_mentor_evaluations_evaluation_periods_evaluation_period_id",
                        column: x => x.evaluation_period_id,
                        principalTable: "evaluation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "peer_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_student_id_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    evaluator_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_student_id_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contribution = table.Column<int>(type: "integer", nullable: false),
                    communication = table.Column<int>(type: "integer", nullable: false),
                    attitude = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_peer_evaluations", x => x.id);
                    table.CheckConstraint("ck_peer_evaluations_scores", "contribution BETWEEN 1 AND 5 AND communication BETWEEN 1 AND 5 AND attitude BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_peer_evaluations_evaluation_periods_evaluation_period_id",
                        column: x => x.evaluation_period_id,
                        principalTable: "evaluation_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_evaluation_periods_status",
                table: "evaluation_periods",
                sql: "status IN ('Open', 'Closed')");

            migrationBuilder.CreateIndex(
                name: "IX_mentor_evaluations_evaluation_period_id_mentor_member_id_ta~",
                table: "mentor_evaluations",
                columns: new[] { "evaluation_period_id", "mentor_member_id", "target_candidate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mentor_evaluations_evaluation_period_id_target_candidate_id",
                table: "mentor_evaluations",
                columns: new[] { "evaluation_period_id", "target_candidate_id" });

            migrationBuilder.CreateIndex(
                name: "IX_peer_evaluations_evaluation_period_id_evaluator_candidate_i~",
                table: "peer_evaluations",
                columns: new[] { "evaluation_period_id", "evaluator_candidate_id", "target_candidate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_peer_evaluations_evaluation_period_id_target_candidate_id",
                table: "peer_evaluations",
                columns: new[] { "evaluation_period_id", "target_candidate_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentor_evaluations");

            migrationBuilder.DropTable(
                name: "peer_evaluations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evaluation_periods_status",
                table: "evaluation_periods");

            migrationBuilder.DropColumn(
                name: "opened_at",
                table: "evaluation_periods");

            migrationBuilder.DropColumn(
                name: "closed_at",
                table: "evaluation_periods");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "starts_at",
                table: "evaluation_periods",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ends_at",
                table: "evaluation_periods",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evaluation_forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    score_max = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    score_min = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    text_max_length = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "evaluation_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reviewer_student_id_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewer_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    target_candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_student_id_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_prompt_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    question_type_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text_value = table.Column<string>(type: "text", nullable: true)
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

            migrationBuilder.AddCheckConstraint(
                name: "ck_evaluation_periods_status",
                table: "evaluation_periods",
                sql: "status IN ('Draft', 'Open', 'Closed')");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_answers_question_id",
                table: "evaluation_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_answers_submission_id",
                table: "evaluation_answers",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_forms_period_id",
                table: "evaluation_forms",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_questions_form_id_order_number",
                table: "evaluation_questions",
                columns: new[] { "form_id", "order_number" },
                unique: true);

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
    }
}
