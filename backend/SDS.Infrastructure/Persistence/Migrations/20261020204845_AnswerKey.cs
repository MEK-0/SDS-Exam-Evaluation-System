// AnswerKey migration'ı cevap anahtarı ve kitapçık tablolarını oluşturur.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnswerKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamAnswerKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaximumScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    DynamicScoring = table.Column<bool>(type: "boolean", nullable: false),
                    AllowMultipleAnswers = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledQuestionPolicy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAnswerKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamAnswerKeys_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamBooklets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamAnswerKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookletCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamBooklets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamBooklets_ExamAnswerKeys_ExamAnswerKeyId",
                        column: x => x.ExamAnswerKeyId,
                        principalTable: "ExamAnswerKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamAnswerKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalQuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamQuestions_ExamAnswerKeys_ExamAnswerKeyId",
                        column: x => x.ExamAnswerKeyId,
                        principalTable: "ExamAnswerKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookletQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamBookletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayQuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    AcceptedAnswers = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookletQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookletQuestions_ExamBooklets_ExamBookletId",
                        column: x => x.ExamBookletId,
                        principalTable: "ExamBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookletQuestions_ExamQuestions_ExamQuestionId",
                        column: x => x.ExamQuestionId,
                        principalTable: "ExamQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestions_ExamBookletId_DisplayQuestionNumber",
                table: "BookletQuestions",
                columns: new[] { "ExamBookletId", "DisplayQuestionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestions_ExamBookletId_ExamQuestionId",
                table: "BookletQuestions",
                columns: new[] { "ExamBookletId", "ExamQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookletQuestions_ExamQuestionId",
                table: "BookletQuestions",
                column: "ExamQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAnswerKeys_ExamId",
                table: "ExamAnswerKeys",
                column: "ExamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamBooklets_ExamAnswerKeyId_BookletCode",
                table: "ExamBooklets",
                columns: new[] { "ExamAnswerKeyId", "BookletCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestions_ExamAnswerKeyId_CanonicalQuestionNumber",
                table: "ExamQuestions",
                columns: new[] { "ExamAnswerKeyId", "CanonicalQuestionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookletQuestions");

            migrationBuilder.DropTable(
                name: "ExamBooklets");

            migrationBuilder.DropTable(
                name: "ExamQuestions");

            migrationBuilder.DropTable(
                name: "ExamAnswerKeys");
        }
    }
}
