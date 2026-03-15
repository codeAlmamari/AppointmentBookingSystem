using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.DTOs.Common;
using AppointmentBookingSystem.DTOs.Staff;
using AppointmentBookingSystem.Middleware;
using AppointmentBookingSystem.Models;
using AppointmentBookingSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Controllers;

/// <summary>
/// Manages staff members and their service type assignments.
/// </summary>
[ApiController]
[Route("api/staff")]
public class StaffController(
    AppDbContext db,
    AuditService audit,
    IMapper mapper) : ControllerBase
{
    /// <summary>
    /// List all staff members — scoped by role.
    /// </summary>
    /// <remarks>
    /// Admin → all staff across all branches.
    /// Manager → own branch only.
    /// </remarks>
    /// <param name="term">Search by full name or email.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="size">Page size (default: 20).</param>
    /// <returns>Paginated list of staff members.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<StaffResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var q = db.Users.Where(u =>
            u.Role == UserRole.STAFF ||
            u.Role == UserRole.BRANCH_MANAGER);

        // Manager sees own branch only
        if (HttpContext.IsManager())
            q = q.Where(u => u.BranchId == HttpContext.GetBranchId());

        // Search
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.ToLower();
            q = q.Where(u =>
                u.FullName.ToLower().Contains(t) ||
                u.Email.ToLower().Contains(t));
        }

        var total = await q.CountAsync();
        var results = await q
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PaginatedResponse<StaffResponse>(
            mapper.Map<List<StaffResponse>>(results), total));
    }

    /// <summary>
    /// Get a single staff member by ID.
    /// </summary>
    /// <param name="id">Staff user ID.</param>
    /// <returns>Staff member details.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var staff = await db.Users.FindAsync(id);

        if (staff == null ||
            (staff.Role != UserRole.STAFF &&
             staff.Role != UserRole.BRANCH_MANAGER))
            return NotFound();

        // Manager scope check
        if (HttpContext.IsManager() && staff.BranchId != HttpContext.GetBranchId())
            return Forbid();

        return Ok(mapper.Map<StaffResponse>(staff));
    }

    /// <summary>
    /// Get all service types assigned to a staff member.
    /// </summary>
    /// <param name="id">Staff user ID.</param>
    /// <returns>List of assigned service types.</returns>
    [HttpGet("{id}/services")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServices(string id)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var staff = await db.Users.FindAsync(id);

        if (staff == null ||
            (staff.Role != UserRole.STAFF &&
             staff.Role != UserRole.BRANCH_MANAGER))
            return NotFound();

        // Manager scope check
        if (HttpContext.IsManager() && staff.BranchId != HttpContext.GetBranchId())
            return Forbid();

        var services = await db.StaffServiceTypes
            .Include(x => x.ServiceType)
            .Where(x => x.StaffId == id)
            .Select(x => new
            {
                x.ServiceType.Id,
                x.ServiceType.Name,
                x.ServiceType.Description,
                x.ServiceType.DurationMinutes,
                x.AssignedAt
            })
            .ToListAsync();

        return Ok(services);
    }

    /// <summary>
    /// Assign service types to a staff member.
    /// </summary>
    /// <remarks>
    /// Manager → own branch only, service must belong to same branch.
    /// Admin → system-wide.
    /// Skips already-assigned service types (idempotent).
    /// </remarks>
    /// <param name="id">Staff user ID.</param>
    /// <returns>Assignment confirmation.</returns>
    [HttpPost("{id}/services")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignServices(
        string id,
        [FromBody] AssignServicesRequest req)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var staff = await db.Users.FindAsync(id);

        if (staff == null ||
            (staff.Role != UserRole.STAFF &&
             staff.Role != UserRole.BRANCH_MANAGER))
            return NotFound(new { error = "Staff member not found" });

        // Manager scope check
        if (HttpContext.IsManager() && staff.BranchId != HttpContext.GetBranchId())
            return Forbid();

        var actorId = HttpContext.GetUserId();
        var role = HttpContext.GetRole();
        var assigned = new List<string>();
        var skipped = new List<string>();

        foreach (var serviceTypeId in req.ServiceTypeIds)
        {
            // Skip if already assigned
            var alreadyAssigned = await db.StaffServiceTypes.AnyAsync(x =>
                x.StaffId == id &&
                x.ServiceTypeId == serviceTypeId);

            if (alreadyAssigned)
            {
                skipped.Add(serviceTypeId);
                continue;
            }

            // Validate service type exists
            var service = await db.ServiceTypes.FindAsync(serviceTypeId);
            if (service == null)
            {
                skipped.Add(serviceTypeId);
                continue;
            }

            // Manager can only assign services from their branch
            if (HttpContext.IsManager() && service.BranchId != HttpContext.GetBranchId())
            {
                skipped.Add(serviceTypeId);
                continue;
            }

            db.StaffServiceTypes.Add(new StaffServiceType
            {
                StaffId = id,
                ServiceTypeId = serviceTypeId
            });

            await audit.LogAsync(
                actorId: actorId,
                actorRole: role,
                actionType: "STAFF_ASSIGNED",
                entityType: "STAFF_SERVICE_TYPE",
                entityId: $"{id}:{serviceTypeId}",
                branchId: staff.BranchId,
                metadata: new { staffId = id, serviceTypeId });

            assigned.Add(serviceTypeId);
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "Service assignment complete",
            assigned,
            skipped
        });
    }

    /// <summary>
    /// Unassign a service type from a staff member.
    /// </summary>
    /// <param name="id">Staff user ID.</param>
    /// <param name="serviceTypeId">Service type ID to unassign.</param>
    /// <returns>Unassignment confirmation.</returns>
    [HttpDelete("{id}/services/{serviceTypeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignService(string id, string serviceTypeId)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var staff = await db.Users.FindAsync(id);

        if (staff == null ||
            (staff.Role != UserRole.STAFF &&
             staff.Role != UserRole.BRANCH_MANAGER))
            return NotFound();

        // Manager scope check
        if (HttpContext.IsManager() && staff.BranchId != HttpContext.GetBranchId())
            return Forbid();

        var link = await db.StaffServiceTypes
            .FirstOrDefaultAsync(x =>
                x.StaffId == id &&
                x.ServiceTypeId == serviceTypeId);

        if (link == null)
            return NotFound(new { error = "Assignment not found" });

        db.StaffServiceTypes.Remove(link);
        await db.SaveChangesAsync();

        await audit.LogAsync(
            actorId: HttpContext.GetUserId(),
            actorRole: HttpContext.GetRole(),
            actionType: "STAFF_UNASSIGNED",
            entityType: "STAFF_SERVICE_TYPE",
            entityId: $"{id}:{serviceTypeId}",
            branchId: staff.BranchId,
            metadata: new { staffId = id, serviceTypeId });

        return Ok(new { message = "Service unassigned successfully" });
    }
}