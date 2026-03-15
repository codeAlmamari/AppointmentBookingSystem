namespace AppointmentBookingSystem.DTOs.Staff
{
    public record StaffResponse(
    string Id,
    string FullName,
    string Email,
    string Username,
    string Role,
    string? BranchId,
    bool IsActive);

    public record AssignServicesRequest(
        List<string> ServiceTypeIds);
}
