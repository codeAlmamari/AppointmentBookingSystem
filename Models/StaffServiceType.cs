namespace AppointmentBookingSystem.Models
{
    public class StaffServiceType
    {
        public string StaffId { get; set; } = null!;
        public User Staff { get; set; } = null!;
        public string ServiceTypeId { get; set; } = null!;
        public ServiceType ServiceType { get; set; } = null!;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
