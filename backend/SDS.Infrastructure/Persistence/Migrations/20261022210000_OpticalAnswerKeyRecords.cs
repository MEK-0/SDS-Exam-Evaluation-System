// Optik TXT içindeki cevap anahtarı satırlarını öğrenci kayıtlarından ayırır.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SDS.Infrastructure.Persistence;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SdsDbContext))]
[Migration("20261022210000_OpticalAnswerKeyRecords")]
public partial class OpticalAnswerKeyRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "IsAnswerKey", table: "OpticalExamRecords", type: "boolean", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsAnswerKey", table: "OpticalExamRecords");
    }
}
