namespace AppointmentBookingSystem.DTOs.ServiceType
{
    public record ServiceTypeResponse(
    string Id,
    string BranchId,
    string Name,
    string? Description,
    int DurationMinutes);

    public record CreateServiceTypeRequest(
        string Name,
        string? Description,
        int DurationMinutes);

    public record UpdateServiceTypeRequest(
        string? Name,
        string? Description,
        int? DurationMinutes,
        bool? IsActive);
}
