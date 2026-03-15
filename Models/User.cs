namespace AppointmentBookingSystem.Models
{
    public class User : BaseEntity
    {
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public UserRole Role { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;

        // Customer-specific
        public string? IdImagePath { get; set; }

        // Branch-scoped roles (Manager / Staff)
        public string? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public ICollection<StaffServiceType> StaffServiceTypes { get; set; } = [];
        public ICollection<Appointment> Appointments { get; set; } = [];
    }
}
