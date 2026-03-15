using System.Globalization;
using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.DTOs.AuditLog;
using AppointmentBookingSystem.DTOs.Common;
using AppointmentBookingSystem.Middleware;
using AppointmentBookingSystem.Models;
using AppointmentBookingSystem.Services;
using AutoMapper;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Controllers;

/// <summary>
/// Admin and manager operations — audit logs, soft delete cleanup,
/// retention settings and queue management.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController(
    AppDbContext db,
    AuditService audit,
    IMapper mapper) : ControllerBase
{
    // Audit Logs

    /// <summary>
    /// List audit logs — scoped by role.
    /// </summary>
    /// <remarks>
    /// Admin → all logs across all branches.
    /// Manager → own branch only.
    /// </remarks>
    /// <param name="term">Search by action type, entity type or actor ID.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="size">Page size (default: 50).</param>
    /// <returns>Paginated list of audit logs.</returns>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(PaginatedResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAuditLogs(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var q = db.AuditLogs.AsQueryable();

        // Manager sees own branch only
        if (HttpContext.IsManager())
            q = q.Where(l => l.BranchId == HttpContext.GetBranchId());

        // Search
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.ToLower();
            q = q.Where(l =>
                l.ActionType.ToLower().Contains(t) ||
                l.EntityType.ToLower().Contains(t) ||
                l.ActorId.ToLower().Contains(t));
        }

        var total = await q.CountAsync();
        var results = await q
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PaginatedResponse<AuditLogResponse>(
            mapper.Map<List<AuditLogResponse>>(results), total));
    }

    /// <summary>
    /// Export all audit logs as a CSV file.
    /// </summary>
    /// <remarks>
    /// Admin only.
    /// </remarks>
    /// <returns>CSV file download.</returns>
    [HttpGet("audit-logs/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportAuditLogs()
    {
        if (!HttpContext.IsAdmin())
            return Forbid();

        var logs = await db.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        var ms = new MemoryStream();

        await using (var writer = new StreamWriter(ms, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            await csv.WriteRecordsAsync(logs);
        }

        ms.Seek(0, SeekOrigin.Begin);

        return File(
            ms,
            "text/csv",
            $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    // Retention Settings

    /// <summary>
    /// Get the current soft delete retention period.
    /// </summary>
    /// <remarks>
    /// Admin only.
    /// </remarks>
    /// <returns>Retention period in days.</returns>
    [HttpGet("settings/retention")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRetention()
    {
        if (!HttpContext.IsAdmin())
            return Forbid();

        var setting = await db.AppSettings
            .FindAsync("soft_delete_retention_days");

        return Ok(new
        {
            retention_days = int.Parse(setting?.Value ?? "30")
        });
    }

    /// <summary>
    /// Update the soft delete retention period.
    /// </summary>
    /// <remarks>
    /// Admin only.
    /// Soft-deleted slots older than this value will be hard-deleted
    /// by the background cleanup service.
    /// </remarks>
    /// <returns>Updated retention period.</returns>
    [HttpPut("settings/retention")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetRetention([FromBody] RetentionRequest req)
    {
        if (!HttpContext.IsAdmin())
            return Forbid();

        if (req.RetentionDays < 1)
            return BadRequest(new { error = "retention_days must be at least 1" });

        var setting = await db.AppSettings
            .FindAsync("soft_delete_retention_days");

        if (setting == null)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = "soft_delete_retention_days",
                Value = req.RetentionDays.ToString(),
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = req.RetentionDays.ToString();
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new { retention_days = req.RetentionDays });
    }

    // Hard Delete Cleanup

    /// <summary>
    /// Manually trigger hard deletion of expired soft-deleted slots.
    /// </summary>
    /// <remarks>
    /// Admin only. Idempotent — safe to run multiple times.
    /// Slots where DeletedAt is older than the retention period will be
    /// permanently removed. Related appointments will have SlotId set to null.
    /// </remarks>
    /// <returns>Number of slots hard-deleted.</returns>
    [HttpDelete("slots/cleanup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CleanupSlots()
    {
        if (!HttpContext.IsAdmin())
            return Forbid();

        // Read retention period
        var setting = await db.AppSettings
            .FindAsync("soft_delete_retention_days");

        var days = int.Parse(setting?.Value ?? "30");
        var cutoff = DateTime.UtcNow.AddDays(-days);

        // Find expired soft-deleted slots
        var expired = await db.Slots
            .Where(s => s.DeletedAt != null && s.DeletedAt <= cutoff)
            .ToListAsync();

        if (expired.Count == 0)
            return Ok(new { message = "Nothing to clean up", deleted = 0 });

        foreach (var slot in expired)
        {
            // Null out SlotId on related appointments
            await db.Appointments
                .Where(a => a.SlotId == slot.Id)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(a => a.SlotId, (string?)null!));

            // Audit log for hard delete
            await audit.LogAsync(
                actorId: HttpContext.GetUserId(),
                actorRole: UserRole.ADMIN,
                actionType: "SLOT_HARD_DELETED",
                entityType: "SLOT",
                entityId: slot.Id,
                branchId: slot.BranchId,
                metadata: new
                {
                    slot.DeletedAt,
                    cutoff,
                    retention_days = days
                });

            db.Slots.Remove(slot);
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "Cleanup complete",
            deleted = expired.Count
        });
    }

    // Queue Position (Bonus #2)

    /// <summary>
    /// Get the live queue status for a branch today.
    /// </summary>
    /// <remarks>
    /// Admin / Manager only.
    /// Manager can only view their own branch queue.
    /// Returns all BOOKED appointments for today ordered by slot start time
    /// with a 1-based queue position for each customer.
    /// </remarks>
    /// <param name="branchId">Branch ID to get queue for.</param>
    /// <returns>Queue length and ordered list with position numbers.</returns>
    [HttpGet("queue/{branchId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQueue(string branchId)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        // Manager can only view their own branch
        if (HttpContext.IsManager() && HttpContext.GetBranchId() != branchId)
            return Forbid();

        // Validate branch exists
        var branchExists = await db.Branches.AnyAsync(b => b.Id == branchId);
        if (!branchExists)
            return NotFound(new { error = "Branch not found" });

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var queue = await db.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Customer)
            .Include(a => a.ServiceType)
            .Where(a =>
                a.BranchId == branchId &&
                a.Status == AppointmentStatus.BOOKED &&
                a.Slot.StartAt >= today &&
                a.Slot.StartAt < tomorrow)
            .OrderBy(a => a.Slot.StartAt)
            .ToListAsync();

        return Ok(new
        {
            branch_id = branchId,
            date = today.ToString("yyyy-MM-dd"),
            queue_length = queue.Count,
            queue = queue.Select((a, index) => new
            {
                position = index + 1,
                appointment_id = a.Id,
                customer = new
                {
                    a.Customer.Id,
                    a.Customer.FullName,
                    a.Customer.Phone
                },
                service = a.ServiceType.Name,
                slot_start = a.Slot.StartAt,
                slot_end = a.Slot.EndAt,
                status = a.Status.ToString()
            })
        });
    }
}

// Request DTOs 
public record RetentionRequest(int RetentionDays);