// Optik TXT içindeki cevap anahtarı satırlarını öğrenci kayıtlarından ayırır.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations;

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
