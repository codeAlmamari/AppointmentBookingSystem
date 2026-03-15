using AppointmentBookingSystem.Models;
using System.Security.Claims;

namespace AppointmentBookingSystem.Middleware;

public static class UserContext
{
    // Identity 
    public static string GetUserId(this HttpContext ctx) =>
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public static string GetUsername(this HttpContext ctx) =>
        ctx.User.FindFirstValue(ClaimTypes.Name)!;

    public static string GetEmail(this HttpContext ctx) =>
        ctx.User.FindFirstValue(ClaimTypes.Email)!;

    public static string GetFullName(this HttpContext ctx) =>
        ctx.User.FindFirstValue("full_name")!;

    public static UserRole GetRole(this HttpContext ctx) =>
        Enum.Parse<UserRole>(ctx.User.FindFirstValue(ClaimTypes.Role)!);

    public static string? GetBranchId(this HttpContext ctx) =>
        ctx.User.FindFirstValue("branch_id");

    // Role checks 
    public static bool IsAdmin(this HttpContext ctx) =>
        ctx.GetRole() == UserRole.ADMIN;

    public static bool IsManager(this HttpContext ctx) =>
        ctx.GetRole() == UserRole.BRANCH_MANAGER;

    public static bool IsStaff(this HttpContext ctx) =>
        ctx.GetRole() == UserRole.STAFF;

    public static bool IsCustomer(this HttpContext ctx) =>
        ctx.GetRole() == UserRole.CUSTOMER;

    public static bool IsAdminOrManager(this HttpContext ctx) =>
        ctx.IsAdmin() || ctx.IsManager();

    public static bool IsStaffOrAbove(this HttpContext ctx) =>
        ctx.IsAdmin() || ctx.IsManager() || ctx.IsStaff();
}
