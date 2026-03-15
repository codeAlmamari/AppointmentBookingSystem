using Microsoft.AspNetCore.Mvc;

namespace AppointmentBookingSystem.DTOs.Auth;

public record RegisterRequest(
    [property: FromForm(Name = "username")] string Username,
    [property: FromForm(Name = "password")] string Password,
    [property: FromForm(Name = "full_name")] string FullName,
    [property: FromForm(Name = "email")] string Email,
    [property: FromForm(Name = "phone")] string? Phone,
    [property: FromForm(Name = "id_image")] IFormFile? IdImage);

public record LoginRequest(
    string Username,
    string Password);

public record AuthResponse(
    string Id,
    string Username,
    string Email,
    string FullName,
    string Role,
    string? BranchId);
