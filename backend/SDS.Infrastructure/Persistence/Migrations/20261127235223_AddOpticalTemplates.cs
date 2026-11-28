using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpticalTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OpticalTemplateVersionId",
                table: "Exams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OpticalTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpticalTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpticalTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
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
                    BlankMarkChar = table.Column<char>(type: "character(1)", nullable: false),
                    DoubleMarkChar = table.Column<char>(type: "character(1)", nullable: false),
                    EncodingMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalTemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpticalTemplateVersions_OpticalTemplates_OpticalTemplateId",
                        column: x => x.OpticalTemplateId,
                        principalTable: "OpticalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_OpticalTemplateVersionId",
                table: "Exams",
                column: "OpticalTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OpticalTemplates_IsDefault",
                table: "OpticalTemplates",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_OpticalTemplates_Name",
                table: "OpticalTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpticalTemplateVersions_OpticalTemplateId_VersionNo",
                table: "OpticalTemplateVersions",
                columns: new[] { "OpticalTemplateId", "VersionNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_OpticalTemplateVersions_OpticalTemplateVersionId",
                table: "Exams",
                column: "OpticalTemplateVersionId",
                principalTable: "OpticalTemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    migration_template_id uuid := gen_random_uuid();
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "Exams") THEN
                        RETURN;
                    END IF;

                    CREATE TEMP TABLE "_OpticalLayouts" AS
                    SELECT
                        "StudentNumberStart", "StudentNumberLength", "StudentNameStart", "StudentNameLength",
                        "BookletTypeStart", "BookletTypeLength", "ExamTypeStart", "ExamTypeLength",
                        "AnswersStart", "AnswersLength",
                        CASE WHEN lower(trim("EncodingName")) = 'windows-1254' THEN 'Windows1254' ELSE 'Utf8' END AS "EncodingMode"
                    FROM "OpticalFormConfigurations"
                    UNION
                    SELECT 0, 9, NULL::integer, NULL::integer, 9, 1, NULL::integer, NULL::integer, 10, 25, 'Utf8'
                    WHERE EXISTS (
                        SELECT 1 FROM "Exams" e
                        WHERE NOT EXISTS (SELECT 1 FROM "OpticalFormConfigurations" c WHERE c."ExamId" = e."Id"));

                    CREATE TEMP TABLE "_OpticalLayoutVersions" AS
                    SELECT
                        gen_random_uuid() AS "Id",
                        (ROW_NUMBER() OVER (ORDER BY "StudentNumberStart", "StudentNumberLength", "StudentNameStart", "StudentNameLength",
                            "BookletTypeStart", "BookletTypeLength", "ExamTypeStart", "ExamTypeLength",
                            "AnswersStart", "AnswersLength", "EncodingMode"))::integer AS "VersionNo",
                        l.*
                    FROM "_OpticalLayouts" l;

                    INSERT INTO "OpticalTemplates" ("Id", "Name", "IsDefault", "CreatedAt")
                    VALUES (migration_template_id, 'Eski sınav düzeni (göç)', false, now());

                    INSERT INTO "OpticalTemplateVersions" (
                        "Id", "OpticalTemplateId", "VersionNo",
                        "StudentNumberStart", "StudentNumberLength", "StudentNameStart", "StudentNameLength",
                        "BookletTypeStart", "BookletTypeLength", "ExamTypeStart", "ExamTypeLength",
                        "AnswersStart", "AnswersLength", "BlankMarkChar", "DoubleMarkChar", "EncodingMode", "CreatedAt")
                    SELECT
                        v."Id", migration_template_id, v."VersionNo",
                        v."StudentNumberStart", v."StudentNumberLength", v."StudentNameStart", v."StudentNameLength",
                        v."BookletTypeStart", v."BookletTypeLength", v."ExamTypeStart", v."ExamTypeLength",
                        v."AnswersStart", v."AnswersLength", ' ', '*', v."EncodingMode", now()
                    FROM "_OpticalLayoutVersions" v;

                    UPDATE "Exams" e
                    SET "OpticalTemplateVersionId" = v."Id"
                    FROM "OpticalFormConfigurations" c
                    JOIN "_OpticalLayoutVersions" v ON
                        v."StudentNumberStart" = c."StudentNumberStart"
                        AND v."StudentNumberLength" = c."StudentNumberLength"
                        AND v."StudentNameStart" IS NOT DISTINCT FROM c."StudentNameStart"
                        AND v."StudentNameLength" IS NOT DISTINCT FROM c."StudentNameLength"
                        AND v."BookletTypeStart" = c."BookletTypeStart"
                        AND v."BookletTypeLength" = c."BookletTypeLength"
                        AND v."ExamTypeStart" IS NOT DISTINCT FROM c."ExamTypeStart"
                        AND v."ExamTypeLength" IS NOT DISTINCT FROM c."ExamTypeLength"
                        AND v."AnswersStart" = c."AnswersStart"
                        AND v."AnswersLength" = c."AnswersLength"
                        AND v."EncodingMode" = CASE WHEN lower(trim(c."EncodingName")) = 'windows-1254' THEN 'Windows1254' ELSE 'Utf8' END
                    WHERE c."ExamId" = e."Id";

                    UPDATE "Exams" e
                    SET "OpticalTemplateVersionId" = v."Id"
                    FROM "_OpticalLayoutVersions" v
                    WHERE e."OpticalTemplateVersionId" IS NULL
                        AND v."StudentNumberStart" = 0 AND v."StudentNumberLength" = 9
                        AND v."StudentNameStart" IS NULL AND v."StudentNameLength" IS NULL
                        AND v."BookletTypeStart" = 9 AND v."BookletTypeLength" = 1
                        AND v."ExamTypeStart" IS NULL AND v."ExamTypeLength" IS NULL
                        AND v."AnswersStart" = 10 AND v."AnswersLength" = 25
                        AND v."EncodingMode" = 'Utf8';

                    DROP TABLE "_OpticalLayoutVersions";
                    DROP TABLE "_OpticalLayouts";
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_OpticalTemplateVersions_OpticalTemplateVersionId",
                table: "Exams");

            migrationBuilder.DropTable(
                name: "OpticalTemplateVersions");

            migrationBuilder.DropTable(
                name: "OpticalTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Exams_OpticalTemplateVersionId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "OpticalTemplateVersionId",
                table: "Exams");
        }
    }
}
