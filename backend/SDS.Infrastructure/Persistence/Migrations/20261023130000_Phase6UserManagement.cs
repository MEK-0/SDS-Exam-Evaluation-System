// Phase 6 kullanıcı yönetimi ve sistem ayarları tablolarını oluşturur.
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SDS.Infrastructure.Persistence;

#nullable disable

namespace SDS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SdsDbContext))]
[Migration("20261023130000_Phase6UserManagement")]
public partial class Phase6UserManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "FirstName", table: "Users", type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "LastName", table: "Users", type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "PersonnelNumber", table: "Users", type: "character varying(32)", maxLength: 32, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Users_PersonnelNumber", table: "Users", column: "PersonnelNumber", unique: true, filter: "\"PersonnelNumber\" IS NOT NULL");
        migrationBuilder.CreateTable(name: "GradingPolicies", columns: table => new { Id = table.Column<Guid>(type: "uuid", nullable: false), AaMinimum = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), BaMinimum = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), BbMinimum = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), CbMinimum = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), CcMinimum = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), DcMinimum = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), MidtermWeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), FinalWeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), MakeUpWeight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false), UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true) }, constraints: table => table.PrimaryKey("PK_GradingPolicies", x => x.Id));
        migrationBuilder.CreateTable(name: "OpticalSystemSettings", columns: table => new { Id = table.Column<Guid>(type: "uuid", nullable: false), StudentNumberStart = table.Column<int>(type: "integer", nullable: false), StudentNumberLength = table.Column<int>(type: "integer", nullable: false), StudentNameStart = table.Column<int>(type: "integer", nullable: true), StudentNameLength = table.Column<int>(type: "integer", nullable: true), BookletTypeStart = table.Column<int>(type: "integer", nullable: false), BookletTypeLength = table.Column<int>(type: "integer", nullable: false), ExamTypeStart = table.Column<int>(type: "integer", nullable: true), ExamTypeLength = table.Column<int>(type: "integer", nullable: true), AnswersStart = table.Column<int>(type: "integer", nullable: false), AnswersLength = table.Column<int>(type: "integer", nullable: false), EncodingName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false), UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true) }, constraints: table => table.PrimaryKey("PK_OpticalSystemSettings", x => x.Id));
        migrationBuilder.CreateTable(name: "PasswordResetRequests", columns: table => new { Id = table.Column<Guid>(type: "uuid", nullable: false), UserId = table.Column<Guid>(type: "uuid", nullable: false), Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false), RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true), ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: true) }, constraints: table => { table.PrimaryKey("PK_PasswordResetRequests", x => x.Id); table.ForeignKey("FK_PasswordResetRequests_Users_ProcessedByUserId", x => x.ProcessedByUserId, "Users", "Id", onDelete: ReferentialAction.Restrict); table.ForeignKey("FK_PasswordResetRequests_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateIndex(name: "IX_PasswordResetRequests_UserId_Status", table: "PasswordResetRequests", columns: new[] { "UserId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GradingPolicies");
        migrationBuilder.DropTable(name: "OpticalSystemSettings");
        migrationBuilder.DropTable(name: "PasswordResetRequests");
        migrationBuilder.DropIndex(name: "IX_Users_PersonnelNumber", table: "Users");
        migrationBuilder.DropColumn(name: "FirstName", table: "Users");
        migrationBuilder.DropColumn(name: "LastName", table: "Users");
        migrationBuilder.DropColumn(name: "PersonnelNumber", table: "Users");
    }
}
