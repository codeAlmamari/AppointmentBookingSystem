namespace AppointmentBookingSystem.Models
{
    public class Appointment : BaseEntity
    {
        public string CustomerId { get; set; } = null!;
        public User Customer { get; set; } = null!;
        public string BranchId { get; set; } = null!;
        public Branch Branch { get; set; } = null!;
        public string ServiceTypeId { get; set; } = null!;
        public ServiceType ServiceType { get; set; } = null!;
        public string SlotId { get; set; } = null!;
        public Slot Slot { get; set; } = null!;
        public string? StaffId { get; set; }
        public User? Staff { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.BOOKED;
        public string? Notes { get; set; }
        public string? AttachmentPath { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
