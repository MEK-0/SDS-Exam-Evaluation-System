// Cevap anahtarı puanlarının eşit dağılımını dört ondalık hassasiyetle saklar.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations;

public partial class UiRevisionAnswerKeyPrecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(name: "Points", table: "ExamQuestions", type: "numeric(8,4)", precision: 8, scale: 4, nullable: false, oldClrType: typeof(decimal), oldType: "numeric(8,2)", oldPrecision: 8, oldScale: 2);
        migrationBuilder.AlterColumn<decimal>(name: "MaximumScore", table: "ExamAnswerKeys", type: "numeric(8,4)", precision: 8, scale: 4, nullable: false, oldClrType: typeof(decimal), oldType: "numeric(8,2)", oldPrecision: 8, oldScale: 2);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(name: "Points", table: "ExamQuestions", type: "numeric(8,2)", precision: 8, scale: 2, nullable: false, oldClrType: typeof(decimal), oldType: "numeric(8,4)", oldPrecision: 8, oldScale: 4);
        migrationBuilder.AlterColumn<decimal>(name: "MaximumScore", table: "ExamAnswerKeys", type: "numeric(8,2)", precision: 8, scale: 2, nullable: false, oldClrType: typeof(decimal), oldType: "numeric(8,4)", oldPrecision: 8, oldScale: 4);
    }
}
