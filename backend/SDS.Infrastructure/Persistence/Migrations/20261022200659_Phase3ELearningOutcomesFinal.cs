using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ELearningOutcomesFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookletQuestionMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceBookletId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceQuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    BookletQuestionNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookletQuestionMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookletQuestionMappings_ExamBooklets_BookletId",
                        column: x => x.BookletId,
                        principalTable: "ExamBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookletQuestionMappings_ExamBooklets_ReferenceBookletId",
                        column: x => x.ReferenceBookletId,
                        principalTable: "ExamBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookletQuestionMappings_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningOutcomes_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionLearningOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningOutcomeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionLearningOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionLearningOutcomes_ExamQuestions_ExamQuestionId",
                        column: x => x.ExamQuestionId,
                        principalTable: "ExamQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestionLearningOutcomes_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestionLearningOutcomes_LearningOutcomes_LearningOutcomeId",
                        column: x => x.LearningOutcomeId,
                        principalTable: "LearningOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestionMappings_BookletId",
                table: "BookletQuestionMappings",
                column: "BookletId");

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestionMappings_ExamId_BookletId_BookletQuestionNum~",
                table: "BookletQuestionMappings",
                columns: new[] { "ExamId", "BookletId", "BookletQuestionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestionMappings_ExamId_ReferenceBookletId_Reference~",
                table: "BookletQuestionMappings",
                columns: new[] { "ExamId", "ReferenceBookletId", "ReferenceQuestionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestionMappings_ReferenceBookletId",
                table: "BookletQuestionMappings",
                column: "ReferenceBookletId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningOutcomes_CourseId_Description",
                table: "LearningOutcomes",
                columns: new[] { "CourseId", "Description" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionLearningOutcomes_ExamId_ExamQuestionId_LearningOutc~",
                table: "QuestionLearningOutcomes",
                columns: new[] { "ExamId", "ExamQuestionId", "LearningOutcomeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionLearningOutcomes_ExamQuestionId",
                table: "QuestionLearningOutcomes",
                column: "ExamQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionLearningOutcomes_LearningOutcomeId",
                table: "QuestionLearningOutcomes",
                column: "LearningOutcomeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookletQuestionMappings");

            migrationBuilder.DropTable(
                name: "QuestionLearningOutcomes");

            migrationBuilder.DropTable(
                name: "LearningOutcomes");
        }
    }
}
