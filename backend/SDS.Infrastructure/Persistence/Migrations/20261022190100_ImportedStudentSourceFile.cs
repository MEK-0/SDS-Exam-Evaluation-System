// Import edilen öğrencinin hangi sınıf listesinden geldiğini saklar.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations;

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
