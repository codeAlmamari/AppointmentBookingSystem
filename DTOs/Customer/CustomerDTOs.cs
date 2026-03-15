namespace AppointmentBookingSystem.DTOs.Customer
{
    public record CustomerResponse(
    string Id,
    string FullName,
    string Email,
    string? Phone,
    string Username,
    bool IsActive,
    DateTime CreatedAt,
    bool HasIdImage);
}
