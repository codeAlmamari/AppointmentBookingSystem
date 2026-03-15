using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.DTOs.Branch;
using AppointmentBookingSystem.DTOs.Common;
using AppointmentBookingSystem.DTOs.ServiceType;
using AppointmentBookingSystem.DTOs.Slot;
using AppointmentBookingSystem.Models;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Controllers;

/// <summary>
/// Public endpoints — no authentication required.
/// </summary>
[ApiController]
[Route("api")]
public class PublicController(AppDbContext db, IMapper mapper) : ControllerBase
{
    /// <summary>
    /// List all active branches.
    /// </summary>
    /// <param name="term">Optional search term — matches branch name or city.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="size">Page size (default: 20).</param>
    /// <returns>Paginated list of branches.</returns>
    [HttpGet("branches")]
    [ProducesResponseType(typeof(PaginatedResponse<BranchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBranches(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var q = db.Branches.Where(b => b.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(b =>
                b.Name.ToLower().Contains(term.ToLower()) ||
                b.City.ToLower().Contains(term.ToLower()));

        var total = await q.CountAsync();
        var results = await q
            .OrderBy(b => b.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PaginatedResponse<BranchResponse>(
            mapper.Map<List<BranchResponse>>(results), total));
    }

    /// <summary>
    /// List all active service types for a specific branch.
    /// </summary>
    /// <param name="branchId">The branch ID to list services for.</param>
    /// <param name="term">Optional search term — matches service name.</param>
    /// <returns>Paginated list of service types.</returns>
    [HttpGet("branches/{branchId}/services")]
    [ProducesResponseType(typeof(PaginatedResponse<ServiceTypeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListServices(
        string branchId,
        [FromQuery] string? term)
    {
        var q = db.ServiceTypes
            .Where(s => s.BranchId == branchId && s.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(s => s.Name.ToLower().Contains(term.ToLower()));

        var results = await q.ToListAsync();

        return Ok(new PaginatedResponse<ServiceTypeResponse>(
            mapper.Map<List<ServiceTypeResponse>>(results), results.Count));
    }

    /// <summary>
    /// List available slots for a branch — only slots with remaining capacity are returned.
    /// </summary>
    /// <param name="branchId">The branch ID to list slots for.</param>
    /// <param name="serviceTypeId">Optional — filter by service type.</param>
    /// <param name="date">Optional — filter by specific date (yyyy-MM-dd).</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="size">Page size (default: 50).</param>
    /// <returns>Paginated list of available slots.</returns>
    [HttpGet("branches/{branchId}/slots")]
    [ProducesResponseType(typeof(PaginatedResponse<SlotResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAvailableSlots(
        string branchId,
        [FromQuery] string? serviceTypeId,
        [FromQuery] DateOnly? date,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50)
    {
        var q = db.Slots
            .Include(s => s.Appointments)
            .Where(s => s.BranchId == branchId &&
                        s.IsActive &&
                        s.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(serviceTypeId))
            q = q.Where(s => s.ServiceTypeId == serviceTypeId);

        if (date.HasValue)
        {
            var from = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var to = from.AddDays(1);
            q = q.Where(s => s.StartAt >= from && s.StartAt < to);
        }

        // Only slots with remaining capacity
        q = q.Where(s =>
            s.Appointments.Count(a => a.Status != AppointmentStatus.CANCELLED) < s.Capacity);

        var total = await q.CountAsync();
        var results = await q
            .OrderBy(s => s.StartAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PaginatedResponse<SlotResponse>(
            mapper.Map<List<SlotResponse>>(results), total));
    }
}