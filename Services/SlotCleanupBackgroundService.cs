using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Services;

public class SlotCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SlotCleanupBackgroundService> logger,
    IConfiguration config) : BackgroundService
{
    // How often to run — configurable from appsettings
    private readonly TimeSpan _interval = TimeSpan.FromHours(
        config.GetValue("SlotCleanup:IntervalHours", 1));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation(
            "Slot cleanup service started. Interval: {Interval}", _interval);

        // Wait 30 seconds after startup before first run
        await Task.Delay(TimeSpan.FromSeconds(30), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during slot cleanup");
            }

            await Task.Delay(_interval, ct);
        }

        logger.LogInformation("Slot cleanup service stopped");
    }

    // Core cleanup logic
    private async Task RunCleanupAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Read retention period from DB
        var setting = await db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "soft_delete_retention_days", ct);

        if (!int.TryParse(setting?.Value, out var days) || days < 1)
        {
            logger.LogWarning("Invalid soft_delete_retention_days — skipping cleanup");
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);

        // Find all expired soft-deleted slots
        var expired = await db.Slots
            .Where(s => s.DeletedAt != null && s.DeletedAt <= cutoff)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            logger.LogDebug("Nothing to clean up (cutoff={Cutoff})", cutoff);
            return;
        }

        logger.LogInformation(
            "Hard-deleting {Count} expired slots (cutoff={Cutoff})",
            expired.Count, cutoff);

        foreach (var slot in expired)
        {
            // Null out SlotId on related appointments — preserve appointment records
            await db.Appointments
                .Where(a => a.SlotId == slot.Id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(a => a.SlotId, (string?)null!), ct);

            // Write audit log — actor is SYSTEM
            db.AuditLogs.Add(new AuditLog
            {
                ActorId = "SYSTEM",
                ActorRole = "SYSTEM",
                ActionType = "SLOT_HARD_DELETED",
                EntityType = "SLOT",
                EntityId = slot.Id,
                BranchId = slot.BranchId,
                Timestamp = DateTime.UtcNow,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    triggered_by = "background_cleanup",
                    deleted_at = slot.DeletedAt,
                    cutoff,
                    retention_days = days
                })
            });

            db.Slots.Remove(slot);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Cleanup complete — {Count} slots deleted", expired.Count);
    }
}