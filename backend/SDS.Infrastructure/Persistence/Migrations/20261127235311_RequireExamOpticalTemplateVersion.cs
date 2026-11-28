using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireExamOpticalTemplateVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpticalFormConfigurations");

            migrationBuilder.DropTable(
                name: "OpticalSystemSettings");

            migrationBuilder.AlterColumn<Guid>(
                name: "OpticalTemplateVersionId",
                table: "Exams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OpticalTemplateVersionId",
                table: "Exams",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "OpticalFormConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswersLength = table.Column<int>(type: "integer", nullable: false),
                    AnswersStart = table.Column<int>(type: "integer", nullable: false),
                    BookletTypeLength = table.Column<int>(type: "integer", nullable: false),
                    BookletTypeStart = table.Column<int>(type: "integer", nullable: false),
                    EncodingName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExamTypeLength = table.Column<int>(type: "integer", nullable: true),
                    ExamTypeStart = table.Column<int>(type: "integer", nullable: true),
                    StudentNameLength = table.Column<int>(type: "integer", nullable: true),
                    StudentNameStart = table.Column<int>(type: "integer", nullable: true),
                    StudentNumberLength = table.Column<int>(type: "integer", nullable: false),
                    StudentNumberStart = table.Column<int>(type: "integer", nullable: false)
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
                name: "OpticalSystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswersLength = table.Column<int>(type: "integer", nullable: false),
                    AnswersStart = table.Column<int>(type: "integer", nullable: false),
                    BookletTypeLength = table.Column<int>(type: "integer", nullable: false),
                    BookletTypeStart = table.Column<int>(type: "integer", nullable: false),
                    EncodingName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExamTypeLength = table.Column<int>(type: "integer", nullable: true),
                    ExamTypeStart = table.Column<int>(type: "integer", nullable: true),
                    StudentNameLength = table.Column<int>(type: "integer", nullable: true),
                    StudentNameStart = table.Column<int>(type: "integer", nullable: true),
                    StudentNumberLength = table.Column<int>(type: "integer", nullable: false),
                    StudentNumberStart = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalSystemSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpticalFormConfigurations_ExamId",
                table: "OpticalFormConfigurations",
                column: "ExamId",
                unique: true);
        }
    }
}
