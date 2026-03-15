using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.DTOs.Appointment;
using AppointmentBookingSystem.DTOs.Common;
using AppointmentBookingSystem.Middleware;
using AppointmentBookingSystem.Models;
using AppointmentBookingSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Controllers;

/// <summary>
/// Manages appointment booking, cancellation, rescheduling and status updates.
/// </summary>
[ApiController]
[Route("api/appointments")]
public class AppointmentsController(
    AppDbContext db,
    AuditService audit,
    FileStorageService files,
    RateLimitService rateLimiter,
    IMapper mapper) : ControllerBase
{
    /// <summary>
    /// Book a new appointment for a slot.
    /// </summary>
    /// <remarks>
    /// Customer only. Optionally attach a file (image or PDF, max 10MB).
    /// Uses multipart/form-data.
    /// </remarks>
    /// <returns>The newly created appointment.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Book([FromForm] BookAppointmentRequest req)
    {
        if (!HttpContext.IsCustomer())
            return Forbid();

        var customerId = HttpContext.GetUserId();

        // Bonus #3 — Rate limit check
        if (!rateLimiter.IsAllowed(customerId, RateLimitAction.Book))
        {
            var (bookings, _) = rateLimiter.GetUsage(customerId);
            return StatusCode(429, new
            {
                error = "Daily booking limit reached",
                limit = rateLimiter.MaxBookingsPerDay,
                used = bookings,
                resets = "midnight UTC"
            });
        }

        // Find slot
        var slot = await db.Slots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s =>
                s.Id == req.SlotId &&
                s.IsActive &&
                s.DeletedAt == null);

        if (slot == null)
            return NotFound(new { error = "Slot not found" });

        // Check capacity
        var activeBookings = slot.Appointments
            .Count(a => a.Status != AppointmentStatus.CANCELLED);

        if (activeBookings >= slot.Capacity)
            return Conflict(new { error = "Slot is fully booked" });

        // Prevent double booking
        if (slot.Appointments.Any(a =>
                a.CustomerId == customerId &&
                a.Status != AppointmentStatus.CANCELLED))
            return Conflict(new { error = "You already have a booking for this slot" });

        // Save optional attachment
        string? attachPath = null;
        if (req.Attachment != null)
        {
            try
            {
                attachPath = await files.SaveAppointmentAttachmentAsync(req.Attachment);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Create appointment
        var appointment = new Appointment
        {
            CustomerId = customerId,
            BranchId = slot.BranchId,
            ServiceTypeId = slot.ServiceTypeId,
            SlotId = slot.Id,
            StaffId = slot.StaffId,
            AttachmentPath = attachPath
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        // Increment rate limiter
        rateLimiter.Increment(customerId, RateLimitAction.Book);

        // Audit log
        await audit.LogAsync(
            actorId: customerId,
            actorRole: UserRole.CUSTOMER,
            actionType: "APPOINTMENT_BOOKED",
            entityType: "APPOINTMENT",
            entityId: appointment.Id,
            branchId: appointment.BranchId,
            metadata: new
            {
                appointment.SlotId,
                appointment.BranchId,
                appointment.ServiceTypeId
            });

        // Reload with navigation properties for mapping
        var created = await db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Slot)
            .Include(a => a.ServiceType)
            .FirstAsync(a => a.Id == appointment.Id);

        return Created(
            $"/api/appointments/{appointment.Id}",
            mapper.Map<AppointmentSummaryResponse>(created));
    }

    /// <summary>
    /// List appointments — scoped by role.
    /// </summary>
    /// <remarks>
    /// Admin → all appointments.
    /// Manager → branch only.
    /// Staff → assigned to me.
    /// Customer → own appointments only.
    /// </remarks>
    /// <param name="term">Search by customer name, email or service name.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="size">Page size (default: 20).</param>
    /// <returns>Paginated list of appointments.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<AppointmentSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var userId = HttpContext.GetUserId();
        var role = HttpContext.GetRole();
        var branchId = HttpContext.GetBranchId();

        var q = db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Slot)
            .Include(a => a.ServiceType)
            .AsQueryable();

        // Scope by role
        q = role switch
        {
            UserRole.CUSTOMER => q.Where(a => a.CustomerId == userId),
            UserRole.STAFF => q.Where(a => a.StaffId == userId),
            UserRole.BRANCH_MANAGER => q.Where(a => a.BranchId == branchId),
            _ => q  // ADMIN sees all
        };

        // Search
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.ToLower();
            q = q.Where(a =>
                a.Customer.FullName.ToLower().Contains(t) ||
                a.Customer.Email.ToLower().Contains(t) ||
                a.ServiceType.Name.ToLower().Contains(t));
        }

        var total = await q.CountAsync();
        var results = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PaginatedResponse<AppointmentSummaryResponse>(
            mapper.Map<List<AppointmentSummaryResponse>>(results), total));
    }

    /// <summary>
    /// Get appointment details by ID.
    /// </summary>
    /// <param name="id">Appointment ID.</param>
    /// <returns>Full appointment details.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AppointmentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id)
    {
        var appointment = await db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Slot)
            .Include(a => a.ServiceType)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
            return NotFound();

        if (!CanAccess(appointment))
            return Forbid();

        return Ok(mapper.Map<AppointmentDetailResponse>(appointment));
    }

    /// <summary>
    /// Download the attachment for an appointment.
    /// </summary>
    /// <param name="id">Appointment ID.</param>
    /// <returns>File stream with correct content type.</returns>
    [HttpGet("{id}/attachment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachment(string id)
    {
        var appointment = await db.Appointments.FindAsync(id);

        if (appointment == null)
            return NotFound();

        if (!CanAccess(appointment))
            return Forbid();

        if (appointment.AttachmentPath == null)
            return NotFound(new { error = "No attachment for this appointment" });

        try
        {
            var (stream, contentType) = files.GetFile(appointment.AttachmentPath);
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = "Attachment file not found on disk" });
        }
    }

    /// <summary>
    /// Cancel an appointment.
    /// </summary>
    /// <remarks>
    /// Customer only — can only cancel own BOOKED appointments.
    /// </remarks>
    /// <param name="id">Appointment ID.</param>
    /// <returns>Cancellation confirmation.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(string id)
    {
        var appointment = await db.Appointments.FindAsync(id);

        if (appointment == null)
            return NotFound();

        if (!HttpContext.IsCustomer() || appointment.CustomerId != HttpContext.GetUserId())
            return Forbid();

        if (appointment.Status != AppointmentStatus.BOOKED)
            return BadRequest(new
            {
                error = $"Cannot cancel an appointment with status {appointment.Status}"
            });

        appointment.Status = AppointmentStatus.CANCELLED;
        appointment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.LogAsync(
            actorId: HttpContext.GetUserId(),
            actorRole: UserRole.CUSTOMER,
            actionType: "APPOINTMENT_CANCELLED",
            entityType: "APPOINTMENT",
            entityId: appointment.Id,
            branchId: appointment.BranchId);

        return Ok(new { message = "Appointment cancelled successfully" });
    }

    /// <summary>
    /// Reschedule an appointment to a different slot.
    /// </summary>
    /// <remarks>
    /// Customer only. New slot must be for the same service type.
    /// </remarks>
    /// <param name="id">Appointment ID.</param>
    /// <returns>Updated appointment.</returns>
    [HttpPut("{id}/reschedule")]
    [ProducesResponseType(typeof(AppointmentSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Reschedule(string id, [FromBody] RescheduleRequest req)
    {
        var appointment = await db.Appointments.FindAsync(id);

        if (appointment == null)
            return NotFound();

        if (!HttpContext.IsCustomer() || appointment.CustomerId != HttpContext.GetUserId())
            return Forbid();

        if (appointment.Status != AppointmentStatus.BOOKED)
            return BadRequest(new { error = "Only BOOKED appointments can be rescheduled" });

        // Bonus #3 — Rate limit check
        var customerId = HttpContext.GetUserId();
        if (!rateLimiter.IsAllowed(customerId, RateLimitAction.Reschedule))
        {
            var (_, reschedules) = rateLimiter.GetUsage(customerId);
            return StatusCode(429, new
            {
                error = "Daily reschedule limit reached",
                limit = rateLimiter.MaxReschedulesPerDay,
                used = reschedules,
                resets = "midnight UTC"
            });
        }

        // Find new slot
        var newSlot = await db.Slots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s =>
                s.Id == req.NewSlotId &&
                s.IsActive &&
                s.DeletedAt == null);

        if (newSlot == null)
            return NotFound(new { error = "New slot not found" });

        // Must be same service type
        if (newSlot.ServiceTypeId != appointment.ServiceTypeId)
            return BadRequest(new { error = "New slot must be for the same service type" });

        // Check capacity
        var activeBookings = newSlot.Appointments
            .Count(a => a.Status != AppointmentStatus.CANCELLED);

        if (activeBookings >= newSlot.Capacity)
            return Conflict(new { error = "New slot is fully booked" });

        // Reschedule
        var oldSlotId = appointment.SlotId;
        appointment.SlotId = newSlot.Id;
        appointment.StaffId = newSlot.StaffId;
        appointment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Increment rate limiter
        rateLimiter.Increment(customerId, RateLimitAction.Reschedule);

        await audit.LogAsync(
            actorId: customerId,
            actorRole: UserRole.CUSTOMER,
            actionType: "APPOINTMENT_RESCHEDULED",
            entityType: "APPOINTMENT",
            entityId: appointment.Id,
            branchId: appointment.BranchId,
            metadata: new { from_slot = oldSlotId, to_slot = newSlot.Id });

        // Reload with navigation properties
        var updated = await db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Slot)
            .Include(a => a.ServiceType)
            .FirstAsync(a => a.Id == appointment.Id);

        return Ok(mapper.Map<AppointmentSummaryResponse>(updated));
    }

    /// <summary>
    /// Update appointment status.
    /// </summary>
    /// <remarks>
    /// Staff / Manager / Admin only.
    /// Allowed statuses: CHECKED_IN, NO_SHOW, COMPLETED.
    /// </remarks>
    /// <param name="id">Appointment ID.</param>
    /// <returns>Updated status.</returns>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusRequest req)
    {
        if (HttpContext.IsCustomer())
            return Forbid();

        var appointment = await db.Appointments.FindAsync(id);

        if (appointment == null)
            return NotFound();

        // Manager scope check
        if (HttpContext.IsManager() && appointment.BranchId != HttpContext.GetBranchId())
            return Forbid();

        // Staff can only update their own appointments
        if (HttpContext.IsStaff() && appointment.StaffId != HttpContext.GetUserId())
            return Forbid();

        // Validate allowed statuses
        var allowed = new[]
        {
            AppointmentStatus.CHECKED_IN,
            AppointmentStatus.NO_SHOW,
            AppointmentStatus.COMPLETED
        };

        if (!allowed.Contains(req.Status))
            return BadRequest(new
            {
                error = "Status must be CHECKED_IN, NO_SHOW or COMPLETED"
            });

        appointment.Status = req.Status;
        appointment.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(req.Notes))
            appointment.Notes = req.Notes;

        await db.SaveChangesAsync();

        await audit.LogAsync(
            actorId: HttpContext.GetUserId(),
            actorRole: HttpContext.GetRole(),
            actionType: "APPOINTMENT_STATUS_UPDATED",
            entityType: "APPOINTMENT",
            entityId: appointment.Id,
            branchId: appointment.BranchId,
            metadata: new { status = req.Status.ToString() });

        return Ok(new
        {
            appointment.Id,
            Status = appointment.Status.ToString(),
            appointment.Notes,
            appointment.UpdatedAt
        });
    }

    // Private helpers

    /// <summary>Checks if the current user can access the given appointment.</summary>
    private bool CanAccess(Appointment appointment)
    {
        var role = HttpContext.GetRole();
        var userId = HttpContext.GetUserId();

        return role switch
        {
            UserRole.ADMIN => true,
            UserRole.BRANCH_MANAGER => appointment.BranchId == HttpContext.GetBranchId(),
            UserRole.STAFF => appointment.StaffId == userId,
            UserRole.CUSTOMER => appointment.CustomerId == userId,
            _ => false
        };
    }
}