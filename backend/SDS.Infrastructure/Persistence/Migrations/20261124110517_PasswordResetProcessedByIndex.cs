// Şifre sıfırlama taleplerini işleyen kullanıcı yabancı anahtarı için eksik indeksi ekler.
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations
{
    public partial class PasswordResetProcessedByIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_ProcessedByUserId",
                table: "PasswordResetRequests",
                column: "ProcessedByUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PasswordResetRequests_ProcessedByUserId",
                table: "PasswordResetRequests");
        }
    }
}
