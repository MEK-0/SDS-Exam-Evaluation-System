// SDS rollerini ve yetki gruplarını tek bir yerde tanımlar.
namespace SDS.Application.Security;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string AdminInstructor = "AdminInstructor";
    public const string Instructor = "Instructor";
    public const string Management = Admin + "," + AdminInstructor;
}
