namespace AppointmentBookingSystem.Models
{
    public class AuditLog : BaseEntity
    {
        public string ActorId { get; set; } = null!;
        public string ActorRole { get; set; } = null!;
        public string ActionType { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public string EntityId { get; set; } = null!;
        public string? BranchId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Metadata { get; set; }  // stored as JSON string
    }
}
