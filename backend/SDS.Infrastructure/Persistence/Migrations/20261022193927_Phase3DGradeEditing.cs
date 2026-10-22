using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3DGradeEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Points",
                table: "ExamQuestions",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)",
                oldPrecision: 8,
                oldScale: 4);

            migrationBuilder.CreateTable(
                name: "ExamResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedStudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    AdjustedScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamResults_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamResults_ImportedStudents_ImportedStudentId",
                        column: x => x.ImportedStudentId,
                        principalTable: "ImportedStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectGradeImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedStudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectGradeImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectGradeImports_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectGradeImports_ImportedStudents_ImportedStudentId",
                        column: x => x.ImportedStudentId,
                        principalTable: "ImportedStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GradeAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    PreviousScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    NewScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeAdjustments_ExamResults_ExamResultId",
                        column: x => x.ExamResultId,
                        principalTable: "ExamResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradeAdjustments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_ExamId_ImportedStudentId",
                table: "ExamResults",
                columns: new[] { "ExamId", "ImportedStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_ImportedStudentId",
                table: "ExamResults",
                column: "ImportedStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeAdjustments_ExamResultId",
                table: "GradeAdjustments",
                column: "ExamResultId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeAdjustments_IdempotencyKey",
                table: "GradeAdjustments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeAdjustments_UserId",
                table: "GradeAdjustments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGradeImports_ExamId_ImportedStudentId",
                table: "ProjectGradeImports",
                columns: new[] { "ExamId", "ImportedStudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGradeImports_ImportedStudentId",
                table: "ProjectGradeImports",
                column: "ImportedStudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeAdjustments");

            migrationBuilder.DropTable(
                name: "ProjectGradeImports");

            migrationBuilder.DropTable(
                name: "ExamResults");

            migrationBuilder.AlterColumn<decimal>(
                name: "Points",
                table: "ExamQuestions",
                type: "numeric(8,2)",
                precision: 8,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,4)",
                oldPrecision: 8,
                oldScale: 4);
        }
    }
}
