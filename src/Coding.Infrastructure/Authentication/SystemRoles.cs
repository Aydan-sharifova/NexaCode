namespace Coding.Infrastructure.Authentication;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Developer = "Developer";
    public const string Guest = "Guest";
    public const string SuperAdmin = "SuperAdmin";
    public const string Moderator = "Moderator";
    public const string User = "User";

    public static readonly string[] All = [SuperAdmin, Admin, Moderator, User];
}
