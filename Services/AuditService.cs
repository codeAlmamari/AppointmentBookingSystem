using System.Text.Json;
using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.Models;

namespace AppointmentBookingSystem.Services;

public class AuditService(AppDbContext db)
{
    public async Task LogAsync(
        string actorId,
        UserRole actorRole,
        string actionType,
        string entityType,
        string entityId,
        string? branchId = null,
        object? metadata = null)
    {
        var log = new AuditLog
        {
            ActorId = actorId,
            ActorRole = actorRole.ToString(),
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            BranchId = branchId,
            Timestamp = DateTime.UtcNow,
            Metadata = metadata != null
                            ? JsonSerializer.Serialize(metadata)
                            : null
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }
}