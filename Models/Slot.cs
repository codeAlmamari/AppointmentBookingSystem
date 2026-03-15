namespace AppointmentBookingSystem.Models
{
    public class Slot : BaseEntity
    {
        public string BranchId { get; set; } = null!;
        public Branch Branch { get; set; } = null!;
        public string ServiceTypeId { get; set; } = null!;
        public ServiceType ServiceType { get; set; } = null!;
        public string? StaffId { get; set; }
        public User? Staff { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int Capacity { get; set; } = 1;
        public bool IsActive { get; set; } = true;

        // Soft delete
        public DateTime? DeletedAt { get; set; }

        public ICollection<Appointment> Appointments { get; set; } = [];
    }
}
