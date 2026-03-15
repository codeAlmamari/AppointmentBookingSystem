using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Middleware;

public class BasicAuthMiddleware(RequestDelegate next)
{
    // Public paths, no auth required 
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/branches",
        "/api/auth/register",
        "/api/auth/login",
        "/swagger",
        "/favicon",
        "/openapi",
        "/api-docs"
    };

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        var path = ctx.Request.Path.Value ?? "";

        // Skip auth for public paths
        if (PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(ctx);
            return;
        }

        // Check Authorization header exists
        if (!ctx.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"FlowCare\"";
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Authorization header required" });
            return;
        }

        try
        {
            // Parse Basic base64(username:password)
            var header = AuthenticationHeaderValue.Parse(authHeader!);

            if (!"Basic".Equals(header.Scheme, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Invalid scheme");

            var credBytes = Convert.FromBase64String(header.Parameter!);
            var creds = Encoding.UTF8.GetString(credBytes).Split(':', 2);

            var username = creds[0];
            var password = creds[1];

            // Find user in database
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

            // Verify password
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsJsonAsync(new { error = "Invalid credentials" });
                return;
            }

            // Build claims from user
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name,           user.Username),
                new(ClaimTypes.Role,           user.Role.ToString()),
                new(ClaimTypes.Email,          user.Email),
                new("full_name",               user.FullName)
            };

            // Add branch claim for scoped roles
            if (user.BranchId != null)
                claims.Add(new Claim("branch_id", user.BranchId));

            // Attach identity to the request
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Basic"));
        }
        catch
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Invalid Authorization header" });
            return;
        }

        await next(ctx);
    }
}