// SDS veritabanı bağlamını ve entity ilişkilerini tanımlar.
using Microsoft.EntityFrameworkCore;
using SDS.Domain.Entities;

namespace SDS.Infrastructure.Persistence;

public sealed class SdsDbContext(DbContextOptions<SdsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseInstructor> CourseInstructors => Set<CourseInstructor>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<OpticalFormConfiguration> OpticalFormConfigurations => Set<OpticalFormConfiguration>();
    public DbSet<ExamImport> ExamImports => Set<ExamImport>();
    public DbSet<ImportedStudent> ImportedStudents => Set<ImportedStudent>();
    public DbSet<OpticalExamRecord> OpticalExamRecords => Set<OpticalExamRecord>();
    public DbSet<ImportValidationIssue> ImportValidationIssues => Set<ImportValidationIssue>();
    public DbSet<ExamAnswerKey> ExamAnswerKeys => Set<ExamAnswerKey>();
    public DbSet<ExamBooklet> ExamBooklets => Set<ExamBooklet>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<BookletQuestion> BookletQuestions => Set<BookletQuestion>();
    public DbSet<StudentMatch> StudentMatches => Set<StudentMatch>();
    public DbSet<StudentMatchAudit> StudentMatchAudits => Set<StudentMatchAudit>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<GradeAdjustment> GradeAdjustments => Set<GradeAdjustment>();
    public DbSet<ProjectGradeImport> ProjectGradeImports => Set<ProjectGradeImport>();
    public DbSet<LearningOutcome> LearningOutcomes => Set<LearningOutcome>();
    public DbSet<QuestionLearningOutcome> QuestionLearningOutcomes => Set<QuestionLearningOutcome>();
    public DbSet<BookletQuestionMapping> BookletQuestionMappings => Set<BookletQuestionMapping>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();
    public DbSet<GradingPolicy> GradingPolicies => Set<GradingPolicy>();
    public DbSet<OpticalSystemSettings> OpticalSystemSettings => Set<OpticalSystemSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Username).IsUnique();
            entity.Property(item => item.Username).HasMaxLength(64).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(item => item.LastName).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PersonnelNumber).HasMaxLength(32);
            entity.HasIndex(item => item.PersonnelNumber).IsUnique().HasFilter("\"PersonnelNumber\" IS NOT NULL");
            entity.Property(item => item.PasswordHash).IsRequired();
            entity.Property(item => item.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        });
        modelBuilder.Entity<AcademicYear>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(32).IsRequired();
        });
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.AcademicYearId, item.Code }).IsUnique();
            entity.Property(item => item.Code).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.HasOne(item => item.AcademicYear).WithMany(item => item.Courses).HasForeignKey(item => item.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseInstructor>(entity =>
        {
            entity.HasKey(item => new { item.CourseId, item.UserId });
            entity.HasOne(item => item.Course).WithMany(item => item.Instructors).HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.User).WithMany(item => item.CourseInstructors).HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.UserId);
        });
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CourseId, item.ExamType });
            entity.Property(item => item.ExamType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(item => item.EvaluationType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(item => item.Course).WithMany(item => item.Exams).HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OpticalFormConfiguration>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ExamId).IsUnique();
            entity.Property(item => item.EncodingName).HasMaxLength(64).IsRequired();
            entity.HasOne(item => item.Exam).WithOne(item => item.OpticalFormConfiguration).HasForeignKey<OpticalFormConfiguration>(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ExamImport>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ExamId).IsUnique();
            entity.Property(item => item.OpticalFileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.ClassListFileName).HasMaxLength(255).IsRequired();
            entity.HasOne(item => item.Exam).WithMany(item => item.Imports).HasForeignKey(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ImportedStudent>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamImportId, item.StudentNumber }).IsUnique();
            entity.Property(item => item.StudentNumber).HasMaxLength(64).IsRequired();
            entity.Property(item => item.FirstName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.LastName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.SourceFileName).HasMaxLength(255).IsRequired();
            entity.HasOne(item => item.ExamImport).WithMany(item => item.ImportedStudents).HasForeignKey(item => item.ExamImportId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OpticalExamRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamImportId, item.LineNumber }).IsUnique();
            entity.Property(item => item.IsAnswerKey).IsRequired();
            entity.Property(item => item.StudentNumber).HasMaxLength(64).IsRequired();
            entity.Property(item => item.BookletType).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Answers).HasMaxLength(4096).IsRequired();
            entity.Property(item => item.ErrorMessage).HasMaxLength(512);
            entity.HasOne(item => item.ExamImport).WithMany(item => item.OpticalExamRecords).HasForeignKey(item => item.ExamImportId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ImportValidationIssue>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ExamImportId);
            entity.Property(item => item.Source).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(512).IsRequired();
            entity.HasOne(item => item.ExamImport).WithMany(item => item.ValidationIssues).HasForeignKey(item => item.ExamImportId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ExamAnswerKey>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ExamId).IsUnique();
            entity.Property(item => item.MaximumScore).HasPrecision(8, 2).IsRequired();
            entity.Property(item => item.CancelledQuestionPolicy).HasConversion<string>().HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(item => item.Exam).WithOne(item => item.AnswerKey).HasForeignKey<ExamAnswerKey>(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ExamBooklet>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamAnswerKeyId, item.BookletCode }).IsUnique();
            entity.Property(item => item.BookletCode).HasMaxLength(16).IsRequired();
            entity.HasOne(item => item.ExamAnswerKey).WithMany(item => item.Booklets).HasForeignKey(item => item.ExamAnswerKeyId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamAnswerKeyId, item.CanonicalQuestionNumber }).IsUnique();
            entity.Property(item => item.Points).HasPrecision(8, 4).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(item => item.ExamAnswerKey).WithMany(item => item.Questions).HasForeignKey(item => item.ExamAnswerKeyId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<BookletQuestion>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamBookletId, item.DisplayQuestionNumber }).IsUnique();
            entity.HasIndex(item => new { item.ExamBookletId, item.ExamQuestionId }).IsUnique();
            entity.Property(item => item.AcceptedAnswers).HasMaxLength(16).IsRequired();
            entity.HasOne(item => item.ExamBooklet).WithMany(item => item.Questions).HasForeignKey(item => item.ExamBookletId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ExamQuestion).WithMany(item => item.BookletQuestions).HasForeignKey(item => item.ExamQuestionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StudentMatch>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamId, item.ImportedStudentId }).IsUnique();
            entity.HasIndex(item => new { item.ExamId, item.OpticalExamRecordId }).IsUnique();
            entity.Property(item => item.MatchType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(item => item.Exam).WithMany().HasForeignKey(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ImportedStudent).WithMany().HasForeignKey(item => item.ImportedStudentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.OpticalExamRecord).WithMany().HasForeignKey(item => item.OpticalExamRecordId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.MatchedByUser).WithMany().HasForeignKey(item => item.MatchedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StudentMatchAudit>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamId, item.OccurredAt });
            entity.Property(item => item.Action).HasMaxLength(32).IsRequired();
            entity.HasOne(item => item.Exam).WithMany().HasForeignKey(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.StudentMatch).WithMany(item => item.Audits).HasForeignKey(item => item.StudentMatchId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ExamResult>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamId, item.ImportedStudentId }).IsUnique();
            entity.Property(item => item.RawScore).HasPrecision(8, 2);
            entity.Property(item => item.AdjustedScore).HasPrecision(8, 2);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(item => item.Exam).WithMany().HasForeignKey(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ImportedStudent).WithMany().HasForeignKey(item => item.ImportedStudentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<GradeAdjustment>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.IdempotencyKey).IsUnique();
            entity.Property(item => item.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(8, 2);
            entity.Property(item => item.PreviousScore).HasPrecision(8, 2);
            entity.Property(item => item.NewScore).HasPrecision(8, 2);
            entity.Property(item => item.IdempotencyKey).HasMaxLength(128);
            entity.HasOne(item => item.ExamResult).WithMany(item => item.Adjustments).HasForeignKey(item => item.ExamResultId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProjectGradeImport>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamId, item.ImportedStudentId }).IsUnique();
            entity.Property(item => item.ProjectScore).HasPrecision(8, 2).IsRequired();
            entity.Property(item => item.SourceFileName).HasMaxLength(255).IsRequired();
            entity.HasOne(item => item.Exam).WithMany().HasForeignKey(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ImportedStudent).WithMany().HasForeignKey(item => item.ImportedStudentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LearningOutcome>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CourseId, item.Description }).IsUnique();
            entity.Property(item => item.Description).HasMaxLength(2000).IsRequired();
            entity.HasOne(item => item.Course).WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<QuestionLearningOutcome>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamId, item.ExamQuestionId, item.LearningOutcomeId }).IsUnique();
            entity.HasOne(item => item.Exam).WithMany(item => item.QuestionLearningOutcomes).HasForeignKey(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ExamQuestion).WithMany(item => item.LearningOutcomeLinks).HasForeignKey(item => item.ExamQuestionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.LearningOutcome).WithMany(item => item.QuestionLinks).HasForeignKey(item => item.LearningOutcomeId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<BookletQuestionMapping>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamId, item.BookletId, item.BookletQuestionNumber }).IsUnique();
            entity.HasIndex(item => new { item.ExamId, item.ReferenceBookletId, item.ReferenceQuestionNumber }).IsUnique();
            entity.HasOne(item => item.Exam).WithMany(item => item.BookletQuestionMappings).HasForeignKey(item => item.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ReferenceBooklet).WithMany(item => item.ReferenceMappings).HasForeignKey(item => item.ReferenceBookletId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Booklet).WithMany(item => item.Mappings).HasForeignKey(item => item.BookletId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PasswordResetRequest>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.Status });
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ProcessedByUser).WithMany().HasForeignKey(item => item.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GradingPolicy>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AaMinimum).HasPrecision(5, 2);
            entity.Property(item => item.BaMinimum).HasPrecision(5, 2);
            entity.Property(item => item.BbMinimum).HasPrecision(5, 2);
            entity.Property(item => item.CbMinimum).HasPrecision(5, 2);
            entity.Property(item => item.CcMinimum).HasPrecision(5, 2);
            entity.Property(item => item.DcMinimum).HasPrecision(5, 2);
            entity.Property(item => item.MidtermWeight).HasPrecision(5, 2);
            entity.Property(item => item.FinalWeight).HasPrecision(5, 2);
            entity.Property(item => item.MakeUpWeight).HasPrecision(5, 2);
        });
        modelBuilder.Entity<OpticalSystemSettings>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EncodingName).HasMaxLength(64).IsRequired();
        });
    }
}
