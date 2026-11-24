// Import edilen öğrencinin hangi sınıf listesinden geldiğini saklar.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SDS.Infrastructure.Persistence;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SdsDbContext))]
[Migration("20261022190100_ImportedStudentSourceFile")]
public partial class ImportedStudentSourceFile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "SourceFileName", table: "ImportedStudents", type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SourceFileName", table: "ImportedStudents");
    }
}
