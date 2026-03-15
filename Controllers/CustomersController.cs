using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.DTOs.Common;
using AppointmentBookingSystem.DTOs.Customer;
using AppointmentBookingSystem.Middleware;
using AppointmentBookingSystem.Models;
using AppointmentBookingSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Controllers;

/// <summary>
/// Manages customer profiles and ID image retrieval.
/// </summary>
[ApiController]
[Route("api/customers")]
public class CustomersController(
    AppDbContext db,
    FileStorageService files,
    IMapper mapper) : ControllerBase
{
    /// <summary>
    /// List all customers.
    /// </summary>
    /// <remarks>
    /// Manager / Admin only.
    /// </remarks>
    /// <param name="term">Search by full name, email or phone.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="size">Page size (default: 20).</param>
    /// <returns>Paginated list of customers.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var q = db.Users.Where(u => u.Role == UserRole.CUSTOMER);

        // Search
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.ToLower();
            q = q.Where(u =>
                u.FullName.ToLower().Contains(t) ||
                u.Email.ToLower().Contains(t) ||
                (u.Phone != null && u.Phone.Contains(t)));
        }

        var total = await q.CountAsync();
        var results = await q
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PaginatedResponse<CustomerResponse>(
            mapper.Map<List<CustomerResponse>>(results), total));
    }

    /// <summary>
    /// Get a single customer by ID.
    /// </summary>
    /// <remarks>
    /// Manager / Admin only.
    /// </remarks>
    /// <param name="id">Customer user ID.</param>
    /// <returns>Customer profile.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        var customer = await db.Users
            .FirstOrDefaultAsync(u =>
                u.Id == id &&
                u.Role == UserRole.CUSTOMER);

        if (customer == null)
            return NotFound();

        return Ok(mapper.Map<CustomerResponse>(customer));
    }

    /// <summary>
    /// Get appointment history for a specific customer.
    /// </summary>
    /// <remarks>
    /// Manager / Admin only.
    /// </remarks>
    /// <param name="id">Customer user ID.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="size">Page size (default: 20).</param>
    /// <returns>Paginated list of customer appointments.</returns>
    [HttpGet("{id}/appointments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointments(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (!HttpContext.IsAdminOrManager())
            return Forbid();

        // Confirm customer exists
        var customerExists = await db.Users.AnyAsync(u =>
            u.Id == id &&
            u.Role == UserRole.CUSTOMER);

        if (!customerExists)
            return NotFound();

        var q = db.Appointments
            .Include(a => a.Slot)
            .Include(a => a.ServiceType)
            .Where(a => a.CustomerId == id);

        // Manager scope check
        if (HttpContext.IsManager())
            q = q.Where(a => a.BranchId == HttpContext.GetBranchId());

        var total = await q.CountAsync();
        var results = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(a => new
            {
                a.Id,
                a.BranchId,
                a.ServiceTypeId,
                a.SlotId,
                a.StaffId,
                Status = a.Status.ToString(),
                a.CreatedAt,
                a.UpdatedAt,
                Slot = new { a.Slot.StartAt, a.Slot.EndAt },
                Service = new { a.ServiceType.Name }
            })
            .ToListAsync();

        return Ok(new PaginatedResponse<object>(results, total));
    }

    /// <summary>
    /// Download the ID image for a customer.
    /// </summary>
    /// <remarks>
    /// Admin only.
    /// </remarks>
    /// <param name="id">Customer user ID.</param>
    /// <returns>Image file stream with correct content type.</returns>
    [HttpGet("{id}/id-image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIdImage(string id)
    {
        // Admin only
        if (!HttpContext.IsAdmin())
            return Forbid();

        var customer = await db.Users
            .FirstOrDefaultAsync(u =>
                u.Id == id &&
                u.Role == UserRole.CUSTOMER);

        if (customer == null)
            return NotFound();

        if (customer.IdImagePath == null)
            return NotFound(new { error = "No ID image on file for this customer" });

        try
        {
            var (stream, contentType) = files.GetFile(customer.IdImagePath);
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = "ID image file not found on disk" });
        }
    }
}