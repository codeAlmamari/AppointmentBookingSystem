namespace AppointmentBookingSystem.DTOs.Slot
{
    public record SlotResponse(
    string Id,
    string BranchId,
    string ServiceTypeId,
    string? StaffId,
    DateTime StartAt,
    DateTime EndAt,
    int Capacity,
    int Booked,
    bool IsActive,
    DateTime? DeletedAt);

    public record SlotInput(
        string ServiceTypeId,
        string? StaffId,
        DateTime StartAt,
        DateTime EndAt,
        int Capacity = 1);

    public record CreateSlotsRequest(
        string? BranchId,
        List<SlotInput> Slots);

    public record UpdateSlotRequest(
        DateTime? StartAt,
        DateTime? EndAt,
        int? Capacity,
        string? StaffId,
        bool? IsActive);

}
