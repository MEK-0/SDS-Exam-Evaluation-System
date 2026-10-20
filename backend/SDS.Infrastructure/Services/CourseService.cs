// Ders ve akademik yıl işlemlerini veritabanı üzerinden yürütür.
using Microsoft.EntityFrameworkCore;
using SDS.Application.Courses;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class CourseService(SdsDbContext dbContext, ICurrentUser currentUser) : ICourseService
{
    public async Task<IReadOnlyCollection<AcademicYearResponse>> GetAcademicYearsAsync(CancellationToken cancellationToken) => await dbContext.AcademicYears.AsNoTracking().OrderByDescending(item => item.Name).Select(item => new AcademicYearResponse(item.Id, item.Name, item.IsActive)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        var query = dbContext.Courses.AsNoTracking().Include(item => item.AcademicYear).Include(item => item.Instructors).Include(item => item.Exams).AsQueryable();
        if (!user.IsAdministrator && user.Id.HasValue) query = query.Where(item => item.Instructors.Any(instructor => instructor.UserId == user.Id.Value));
        var courses = await query.OrderBy(item => item.Code).ToListAsync(cancellationToken);
        return courses.Select(InfrastructureServiceSupport.ToResponse).ToList();
    }

    public async Task<CourseResponse?> GetCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        var course = await dbContext.Courses.AsNoTracking().Include(item => item.AcademicYear).Include(item => item.Instructors).Include(item => item.Exams).SingleOrDefaultAsync(item => item.Id == courseId, cancellationToken);
        if (course is null || (!user.IsAdministrator && !course.Instructors.Any(item => item.UserId == user.Id))) return null;
        return InfrastructureServiceSupport.ToResponse(course);
    }

    public async Task<CourseResponse> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (!await dbContext.AcademicYears.AnyAsync(item => item.Id == request.AcademicYearId, cancellationToken)) throw new ArgumentException("Akademik yıl bulunamadı.");
        if (await dbContext.Courses.AnyAsync(item => item.AcademicYearId == request.AcademicYearId && item.Code == normalizedCode, cancellationToken)) throw new InvalidOperationException("Bu akademik yılda aynı ders kodu zaten kullanılıyor.");
        var course = new Course { Id = Guid.NewGuid(), Code = normalizedCode, Name = request.Name.Trim(), AcademicYearId = request.AcademicYearId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(course).Reference(item => item.AcademicYear).LoadAsync(cancellationToken);
        return InfrastructureServiceSupport.ToResponse(course);
    }

    public async Task<CourseResponse?> UpdateCourseAsync(Guid courseId, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var course = await dbContext.Courses.Include(item => item.AcademicYear).Include(item => item.Instructors).Include(item => item.Exams).SingleOrDefaultAsync(item => item.Id == courseId, cancellationToken);
        if (course is null) return null;
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.Courses.AnyAsync(item => item.Id != courseId && item.AcademicYearId == request.AcademicYearId && item.Code == normalizedCode, cancellationToken)) throw new InvalidOperationException("Bu akademik yılda aynı ders kodu zaten kullanılıyor.");
        course.Code = normalizedCode;
        course.Name = request.Name.Trim();
        course.AcademicYearId = request.AcademicYearId;
        course.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return InfrastructureServiceSupport.ToResponse(course);
    }

    public async Task<bool> AssignInstructorAsync(Guid courseId, AssignInstructorRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Courses.AnyAsync(item => item.Id == courseId, cancellationToken) || !await dbContext.Users.AnyAsync(item => item.Id == request.UserId && item.Role != UserRole.Admin, cancellationToken)) return false;
        if (!await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == request.UserId, cancellationToken)) dbContext.CourseInstructors.Add(new CourseInstructor { CourseId = courseId, UserId = request.UserId });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
