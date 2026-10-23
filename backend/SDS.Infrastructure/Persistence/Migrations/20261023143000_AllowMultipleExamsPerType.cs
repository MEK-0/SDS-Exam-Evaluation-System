// Aynı ders ve sınav türü için yeni sınav kayıtlarına izin verir.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SDS.Infrastructure.Persistence;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SdsDbContext))]
[Migration("20261023143000_AllowMultipleExamsPerType")]
public partial class AllowMultipleExamsPerType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Exams_CourseId_ExamType", table: "Exams");
        migrationBuilder.CreateIndex(name: "IX_Exams_CourseId_ExamType", table: "Exams", columns: new[] { "CourseId", "ExamType" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Exams_CourseId_ExamType", table: "Exams");
        migrationBuilder.CreateIndex(name: "IX_Exams_CourseId_ExamType", table: "Exams", columns: new[] { "CourseId", "ExamType" }, unique: true);
    }
}
