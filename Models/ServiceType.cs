namespace AppointmentBookingSystem.Models
{
    public class ServiceType : BaseEntity
    {
        public string BranchId { get; set; } = null!;
        public Branch Branch { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Slot> Slots { get; set; } = [];
        public ICollection<StaffServiceType> StaffServiceTypes { get; set; } = [];
    }
}
