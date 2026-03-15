using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.DTOs.Common;
using AppointmentBookingSystem.DTOs.Slot;
using AppointmentBookingSystem.Middleware;
using AppointmentBookingSystem.Models;
using AppointmentBookingSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Controllers;

/// <summary>
/// Manages time slots — creation, updates, and soft deletion.
/// </summary>
[ApiController]
[Route("api/slots")]
public class SlotsController(
    AppDbContext db,
    AuditService audit,
    IMapper mapper) : ControllerBase
{
    /// <summary>
    /// Create one or multiple slots for a branch.
    /// </summary>
    /// <remarks>
    /// Manager → can only create slots for their own branch.
    /// Admin → can create slots for any branch (must provide BranchId).
    /// </remarks>
    /// <returns>List of created slots.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(List<SlotResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateSlotsRequest req)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        // Resolve branch ID
        var branchId = HttpContext.IsAdmin()
            ? req.BranchId
            : HttpContext.GetBranchId();

        if (string.IsNullOrWhiteSpace(branchId))
            return BadRequest(new { error = "BranchId is required" });

        // Manager cannot create slots for other branches
        if (HttpContext.IsManager() && branchId != HttpContext.GetBranchId())
            return Forbid();

        // Validate branch exists
        var branchExists = await db.Branches.AnyAsync(b => b.Id == branchId);
        if (!branchExists)
            return NotFound(new { error = "Branch not found" });

        var actorId = HttpContext.GetUserId();
        var role = HttpContext.GetRole();
        var created = new List<Slot>();

        foreach (var input in req.Slots)
        {
            // Validate service type belongs to branch
            var serviceExists = await db.ServiceTypes.AnyAsync(s =>
                s.Id == input.ServiceTypeId &&
                s.BranchId == branchId &&
                s.IsActive);

            if (!serviceExists)
            {
                return BadRequest(new
                {
                    error = $"ServiceType '{input.ServiceTypeId}' not found in branch '{branchId}'"
                });
            }

            var slot = new Slot
            {
                BranchId = branchId,
                ServiceTypeId = input.ServiceTypeId,
                StaffId = input.StaffId,
                StartAt = input.StartAt.ToUniversalTime(),
                EndAt = input.EndAt.ToUniversalTime(),
                Capacity = input.Capacity > 0 ? input.Capacity : 1
            };

            db.Slots.Add(slot);
            await db.SaveChangesAsync();

            await audit.LogAsync(
                actorId: actorId,
                actorRole: role,
                actionType: "SLOT_CREATED",
                entityType: "SLOT",
                entityId: slot.Id,
                branchId: branchId,
                metadata: new
                {
                    slot.ServiceTypeId,
                    slot.StartAt,
                    slot.EndAt,
                    slot.Capacity
                });

            created.Add(slot);
        }

        return Created("/api/slots", mapper.Map<List<SlotResponse>>(created));
    }

    /// <summary>
    /// List slots — scoped by role.
    /// </summary>
    /// <remarks>
    /// Admin → all slots including soft-deleted (use ?includeDeleted=true).
    /// Manager → own branch only.
    /// </remarks>
    /// <param name="branchId">Admin only — filter by branch.</param>
    /// <param name="includeDeleted">Admin only — include soft-deleted slots.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="size">Page size (default: 20).</param>
    /// <returns>Paginated list of slots.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<SlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? branchId,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var q = db.Slots
            .Include(s => s.ServiceType)
            .Include(s => s.Appointments)
            .AsQueryable();

        // Scope by role
        if (HttpContext.IsManager())
            q = q.Where(s => s.BranchId == HttpContext.GetBranchId());
        else if (!string.IsNullOrWhiteSpace(branchId))
            q = q.Where(s => s.BranchId == branchId);

        // Only admins can see soft-deleted slots
        if (!includeDeleted || !HttpContext.IsAdmin())
            q = q.Where(s => s.DeletedAt == null);

        var total = await q.CountAsync();
        var results = await q
            .OrderBy(s => s.StartAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PaginatedResponse<SlotResponse>(
            mapper.Map<List<SlotResponse>>(results), total));
    }

    /// <summary>
    /// Get a single slot by ID.
    /// </summary>
    /// <param name="id">Slot ID.</param>
    /// <returns>Slot details.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SlotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var slot = await db.Slots
            .Include(s => s.ServiceType)
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (slot == null)
            return NotFound();

        // Manager scope check
        if (HttpContext.IsManager() && slot.BranchId != HttpContext.GetBranchId())
            return Forbid();

        return Ok(mapper.Map<SlotResponse>(slot));
    }

    /// <summary>
    /// Update a slot.
    /// </summary>
    /// <remarks>
    /// Manager → own branch only.
    /// Cannot update soft-deleted slots.
    /// </remarks>
    /// <param name="id">Slot ID.</param>
    /// <returns>Updated slot.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(SlotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateSlotRequest req)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var slot = await db.Slots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (slot == null || slot.DeletedAt != null)
            return NotFound();

        // Manager scope check
        if (HttpContext.IsManager() && slot.BranchId != HttpContext.GetBranchId())
            return Forbid();

        // Apply updates
        if (req.StartAt.HasValue) slot.StartAt = req.StartAt.Value.ToUniversalTime();
        if (req.EndAt.HasValue) slot.EndAt = req.EndAt.Value.ToUniversalTime();
        if (req.Capacity.HasValue) slot.Capacity = req.Capacity.Value;
        if (req.StaffId != null) slot.StaffId = req.StaffId;
        if (req.IsActive.HasValue) slot.IsActive = req.IsActive.Value;

        await db.SaveChangesAsync();

        await audit.LogAsync(
            actorId: HttpContext.GetUserId(),
            actorRole: HttpContext.GetRole(),
            actionType: "SLOT_UPDATED",
            entityType: "SLOT",
            entityId: slot.Id,
            branchId: slot.BranchId,
            metadata: new
            {
                slot.StartAt,
                slot.EndAt,
                slot.Capacity,
                slot.IsActive
            });

        return Ok(mapper.Map<SlotResponse>(slot));
    }

    /// <summary>
    /// Soft-delete a slot.
    /// </summary>
    /// <remarks>
    /// Sets DeletedAt timestamp and IsActive = false.
    /// Slot will be hard-deleted after the retention period by the background service.
    /// Manager → own branch only.
    /// </remarks>
    /// <param name="id">Slot ID.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(string id)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var slot = await db.Slots.FindAsync(id);

        if (slot == null || slot.DeletedAt != null)
            return NotFound();

        // Manager scope check
        if (HttpContext.IsManager() && slot.BranchId != HttpContext.GetBranchId())
            return Forbid();

        // Soft delete
        slot.DeletedAt = DateTime.UtcNow;
        slot.IsActive = false;
        await db.SaveChangesAsync();

        await audit.LogAsync(
            actorId: HttpContext.GetUserId(),
            actorRole: HttpContext.GetRole(),
            actionType: "SLOT_DELETED",
            entityType: "SLOT",
            entityId: slot.Id,
            branchId: slot.BranchId,
            metadata: new { slot.DeletedAt });

        return Ok(new
        {
            message = "Slot soft-deleted successfully",
            id = slot.Id,
            deletedAt = slot.DeletedAt
        });
    }
}