// Öğrenci eşleştirme tabloları ve sınav durum alanları için migration tanımlarını içerir.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MatchingCalculated",
                table: "Exams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MatchingConfirmed",
                table: "Exams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MatchingUpdatedAt",
                table: "Exams",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudentMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedStudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpticalExamRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsAbsent = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MatchedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentMatches_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentMatches_ImportedStudents_ImportedStudentId",
                        column: x => x.ImportedStudentId,
                        principalTable: "ImportedStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentMatches_OpticalExamRecords_OpticalExamRecordId",
                        column: x => x.OpticalExamRecordId,
                        principalTable: "OpticalExamRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentMatches_Users_MatchedByUserId",
                        column: x => x.MatchedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentMatchAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousImportedStudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousOpticalExamRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewImportedStudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewOpticalExamRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMatchAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentMatchAudits_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentMatchAudits_StudentMatches_StudentMatchId",
                        column: x => x.StudentMatchId,
                        principalTable: "StudentMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentMatchAudits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentMatchAudits_ExamId_OccurredAt",
                table: "StudentMatchAudits",
                columns: new[] { "ExamId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentMatchAudits_StudentMatchId",
                table: "StudentMatchAudits",
                column: "StudentMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMatchAudits_UserId",
                table: "StudentMatchAudits",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMatches_ExamId_ImportedStudentId",
                table: "StudentMatches",
                columns: new[] { "ExamId", "ImportedStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentMatches_ExamId_OpticalExamRecordId",
                table: "StudentMatches",
                columns: new[] { "ExamId", "OpticalExamRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentMatches_ImportedStudentId",
                table: "StudentMatches",
                column: "ImportedStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMatches_MatchedByUserId",
                table: "StudentMatches",
                column: "MatchedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMatches_OpticalExamRecordId",
                table: "StudentMatches",
                column: "OpticalExamRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentMatchAudits");

            migrationBuilder.DropTable(
                name: "StudentMatches");

            migrationBuilder.DropColumn(
                name: "MatchingCalculated",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "MatchingConfirmed",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "MatchingUpdatedAt",
                table: "Exams");
        }
    }
}
