using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionCancellationAndScaledScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookletQuestionMappings_ExamId_ReferenceBookletId_Reference~",
                table: "BookletQuestionMappings");

            migrationBuilder.AddColumn<decimal>(
                name: "ManualScore",
                table: "ExamResults",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ScaledScore",
                table: "ExamResults",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ExamQuestions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Optical");

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicy",
                table: "BookletQuestions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "AwardFullPoints");

            migrationBuilder.AddColumn<decimal>(
                name: "Points",
                table: "BookletQuestions",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BookletQuestions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");

            // Veri taşıma: mevcut iptal durumu ve 100 üzerinden karşılık yeni sütunlara aktarılır (EF üretemez).
            migrationBuilder.Sql("UPDATE \"BookletQuestions\" AS b SET \"Status\" = 'Cancelled' FROM \"ExamQuestions\" AS q WHERE q.\"Id\" = b.\"ExamQuestionId\" AND q.\"Status\" = 'Cancelled';");
            migrationBuilder.Sql("UPDATE \"ExamResults\" AS r SET \"ScaledScore\" = ROUND(r.\"RawScore\" * 100 / k.\"MaximumScore\", 2) FROM \"ExamAnswerKeys\" AS k WHERE k.\"ExamId\" = r.\"ExamId\" AND r.\"RawScore\" IS NOT NULL AND k.\"MaximumScore\" > 0;");

            migrationBuilder.CreateTable(
                name: "QuestionCancellationSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBookletCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceQuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    TargetBookletCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetQuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    Policy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionCancellationSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionCancellationSuggestions_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestionMappings_ExamId_ReferenceBookletId_BookletId~",
                table: "BookletQuestionMappings",
                columns: new[] { "ExamId", "ReferenceBookletId", "BookletId", "ReferenceQuestionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionCancellationSuggestions_ExamId_SourceBookletCode_So~",
                table: "QuestionCancellationSuggestions",
                columns: new[] { "ExamId", "SourceBookletCode", "SourceQuestionNumber", "TargetBookletCode", "TargetQuestionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionCancellationSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_BookletQuestionMappings_ExamId_ReferenceBookletId_BookletId~",
                table: "BookletQuestionMappings");

            migrationBuilder.DropColumn(
                name: "ManualScore",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "ScaledScore",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ExamQuestions");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy",
                table: "BookletQuestions");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "BookletQuestions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BookletQuestions");

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestionMappings_ExamId_ReferenceBookletId_Reference~",
                table: "BookletQuestionMappings",
                columns: new[] { "ExamId", "ReferenceBookletId", "ReferenceQuestionNumber" },
                unique: true);
        }
    }
}
