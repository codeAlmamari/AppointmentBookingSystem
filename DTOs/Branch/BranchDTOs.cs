namespace AppointmentBookingSystem.DTOs.Branch
{
    public record BranchResponse(
    string Id,
    string Name,
    string City,
    string Address,
    string Timezone);

    public record CreateBranchRequest(
        string Name,
        string City,
        string Address,
        string Timezone);

    public record UpdateBranchRequest(
        string? Name,
        string? City,
        string? Address,
        string? Timezone,
        bool? IsActive);
}
