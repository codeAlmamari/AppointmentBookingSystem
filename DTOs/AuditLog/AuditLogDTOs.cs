namespace AppointmentBookingSystem.DTOs.AuditLog
{
    public record AuditLogResponse(
    string Id,
    string ActorId,
    string ActorRole,
    string ActionType,
    string EntityType,
    string EntityId,
    string? BranchId,
    DateTime Timestamp,
    string? Metadata);
}
