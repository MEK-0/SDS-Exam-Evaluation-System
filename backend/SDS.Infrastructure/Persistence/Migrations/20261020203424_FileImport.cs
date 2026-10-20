// FileImport migration'ı dosya import tablolarını oluşturur.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FileImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpticalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ClassListFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalOpticalRecordCount = table.Column<int>(type: "integer", nullable: false),
                    ValidOpticalRecordCount = table.Column<int>(type: "integer", nullable: false),
                    InvalidOpticalRecordCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamImports_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpticalFormConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentNumberStart = table.Column<int>(type: "integer", nullable: false),
                    StudentNumberLength = table.Column<int>(type: "integer", nullable: false),
                    StudentNameStart = table.Column<int>(type: "integer", nullable: true),
                    StudentNameLength = table.Column<int>(type: "integer", nullable: true),
                    BookletTypeStart = table.Column<int>(type: "integer", nullable: false),
                    BookletTypeLength = table.Column<int>(type: "integer", nullable: false),
                    ExamTypeStart = table.Column<int>(type: "integer", nullable: true),
                    ExamTypeLength = table.Column<int>(type: "integer", nullable: true),
                    AnswersStart = table.Column<int>(type: "integer", nullable: false),
                    AnswersLength = table.Column<int>(type: "integer", nullable: false),
                    EncodingName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalFormConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpticalFormConfigurations_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportedStudents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LastName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportedStudents_ExamImports_ExamImportId",
                        column: x => x.ExamImportId,
                        principalTable: "ExamImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportValidationIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportValidationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportValidationIssues_ExamImports_ExamImportId",
                        column: x => x.ExamImportId,
                        principalTable: "ExamImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpticalExamRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    StudentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BookletType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Answers = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalExamRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpticalExamRecords_ExamImports_ExamImportId",
                        column: x => x.ExamImportId,
                        principalTable: "ExamImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamImports_ExamId",
                table: "ExamImports",
                column: "ExamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedStudents_ExamImportId_StudentNumber",
                table: "ImportedStudents",
                columns: new[] { "ExamImportId", "StudentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportValidationIssues_ExamImportId",
                table: "ImportValidationIssues",
                column: "ExamImportId");

            migrationBuilder.CreateIndex(
                name: "IX_OpticalExamRecords_ExamImportId_LineNumber",
                table: "OpticalExamRecords",
                columns: new[] { "ExamImportId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpticalFormConfigurations_ExamId",
                table: "OpticalFormConfigurations",
                column: "ExamId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportedStudents");

            migrationBuilder.DropTable(
                name: "ImportValidationIssues");

            migrationBuilder.DropTable(
                name: "OpticalExamRecords");

            migrationBuilder.DropTable(
                name: "OpticalFormConfigurations");

            migrationBuilder.DropTable(
                name: "ExamImports");
        }
    }
}
