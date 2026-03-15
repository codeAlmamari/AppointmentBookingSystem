using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.DTOs.Auth;
using AppointmentBookingSystem.Models;
using AppointmentBookingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Controllers;

/// <summary>
/// Handles customer registration and login.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    FileStorageService files,
    AuditService audit) : ControllerBase
{
    /// <summary>
    /// Register a new customer account.
    /// </summary>
    /// <remarks>
    /// Requires a valid ID image (JPEG, PNG, GIF, WebP — max 5MB).
    /// Uses multipart/form-data.
    /// </remarks>
    /// <returns>The newly created customer profile.</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromForm] RegisterRequest req)
    {
        // Check username is not taken
        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { error = "Username already taken" });

        // Check email is not taken
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { error = "Email already registered" });

        // ID image is required
        if (req.IdImage == null)
            return BadRequest(new { error = "ID image is required" });

        // Save ID image
        string imagePath;
        try
        {
            imagePath = await files.SaveCustomerIdImageAsync(req.IdImage);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        // Create user
        var user = new User
        {
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = UserRole.CUSTOMER,
            FullName = req.FullName,
            Email = req.Email,
            Phone = req.Phone,
            IdImagePath = imagePath
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Audit log
        await audit.LogAsync(
            actorId: user.Id,
            actorRole: UserRole.CUSTOMER,
            actionType: "CUSTOMER_REGISTERED",
            entityType: "USER",
            entityId: user.Id,
            metadata: new { user.Username, user.Email });

        return Created($"/api/customers/{user.Id}", new AuthResponse(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.ToString(),
            BranchId: user.BranchId));
    }

    /// <summary>
    /// Validate credentials and return user profile.
    /// </summary>
    /// <remarks>
    /// This endpoint confirms credentials are valid.
    /// For all other requests use HTTP Basic Auth header:
    /// Authorization: Basic base64(username:password)
    /// </remarks>
    /// <returns>User profile with role information.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // Find user
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);

        // Verify password
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid username or password" });

        return Ok(new AuthResponse(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.ToString(),
            BranchId: user.BranchId));
    }
}