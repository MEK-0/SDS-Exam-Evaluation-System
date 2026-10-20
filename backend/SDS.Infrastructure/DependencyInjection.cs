// Infrastructure servislerinin dependency injection kaydını sağlar.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SDS.Infrastructure.Persistence;
using SDS.Application.Auth;
using SDS.Application.Courses;
using SDS.Application.Exams;
using SDS.Application.Imports;
using SDS.Application.AnswerKeys;
using SDS.Infrastructure.Importing;
using SDS.Infrastructure.Seeding;
using SDS.Infrastructure.Services;

namespace SDS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SdsDatabase") ?? "Host=localhost;Port=5432;Database=sds;Username=sds";
        services.AddDbContext<SdsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IOpticalFileParser, OpticalFileParser>();
        services.AddScoped<IClassListParser, ClassListParser>();
        services.AddScoped<IStudentDataImportService, StudentDataImportService>();
        services.AddScoped<IAnswerKeyService, AnswerKeyService>();
        services.AddScoped<DevelopmentSeeder>();
        return services;
    }
}
