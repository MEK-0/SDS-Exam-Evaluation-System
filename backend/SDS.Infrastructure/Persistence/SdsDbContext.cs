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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Username).IsUnique();
            entity.Property(item => item.Username).HasMaxLength(64).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
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
            entity.HasIndex(item => new { item.CourseId, item.ExamType }).IsUnique();
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
            entity.HasOne(item => item.ExamImport).WithMany(item => item.ImportedStudents).HasForeignKey(item => item.ExamImportId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OpticalExamRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ExamImportId, item.LineNumber }).IsUnique();
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
            entity.Property(item => item.Points).HasPrecision(8, 2).IsRequired();
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
    }
}
