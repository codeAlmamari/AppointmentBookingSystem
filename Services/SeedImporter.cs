using System.Text.Json;
using System.Text.Json.Serialization;
using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBookingSystem.Services;

public class SeedImporter(
    AppDbContext db,
    ILogger<SeedImporter> logger)
{
    // JSON shape

    private sealed class SeedFile
    {
        [JsonPropertyName("users")] public SeedUsers Users { get; set; } = new();
        [JsonPropertyName("branches")] public List<SeedBranch> Branches { get; set; } = [];
        [JsonPropertyName("service_types")] public List<SeedSvc> ServiceTypes { get; set; } = [];
        [JsonPropertyName("staff_service_types")] public List<SeedSST> StaffServiceTypes { get; set; } = [];
        [JsonPropertyName("slots")] public List<SeedSlot> Slots { get; set; } = [];
        [JsonPropertyName("appointments")] public List<SeedAppt> Appointments { get; set; } = [];
        [JsonPropertyName("audit_logs")] public List<SeedAudit> AuditLogs { get; set; } = [];
    }

    private sealed class SeedUsers
    {
        [JsonPropertyName("admin")] public List<SeedUser> Admin { get; set; } = [];
        [JsonPropertyName("branch_managers")] public List<SeedUser> BranchManagers { get; set; } = [];
        [JsonPropertyName("staff")] public List<SeedUser> Staff { get; set; } = [];
        [JsonPropertyName("customers")] public List<SeedUser> Customers { get; set; } = [];
    }

    private sealed class SeedUser
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("username")] public string Username { get; set; } = "";
        [JsonPropertyName("password")] public string Password { get; set; } = "";
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("full_name")] public string FullName { get; set; } = "";
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        [JsonPropertyName("branch_id")] public string? BranchId { get; set; }
        [JsonPropertyName("is_active")] public bool IsActive { get; set; } = true;
    }

    private sealed class SeedBranch
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("city")] public string City { get; set; } = "";
        [JsonPropertyName("address")] public string Address { get; set; } = "";
        [JsonPropertyName("timezone")] public string Timezone { get; set; } = "Asia/Muscat";
        [JsonPropertyName("is_active")] public bool IsActive { get; set; } = true;
    }

    private sealed class SeedSvc
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("branch_id")] public string BranchId { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("duration_minutes")] public int DurationMinutes { get; set; }
        [JsonPropertyName("is_active")] public bool IsActive { get; set; } = true;
    }

    private sealed class SeedSST
    {
        [JsonPropertyName("staff_id")] public string StaffId { get; set; } = "";
        [JsonPropertyName("service_type_id")] public string ServiceTypeId { get; set; } = "";
    }

    private sealed class SeedSlot
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("branch_id")] public string BranchId { get; set; } = "";
        [JsonPropertyName("service_type_id")] public string ServiceTypeId { get; set; } = "";
        [JsonPropertyName("staff_id")] public string? StaffId { get; set; }
        [JsonPropertyName("start_at")] public DateTime StartAt { get; set; }
        [JsonPropertyName("end_at")] public DateTime EndAt { get; set; }
        [JsonPropertyName("capacity")] public int Capacity { get; set; } = 1;
        [JsonPropertyName("is_active")] public bool IsActive { get; set; } = true;
    }

    private sealed class SeedAppt
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("customer_id")] public string CustomerId { get; set; } = "";
        [JsonPropertyName("branch_id")] public string BranchId { get; set; } = "";
        [JsonPropertyName("service_type_id")] public string ServiceTypeId { get; set; } = "";
        [JsonPropertyName("slot_id")] public string SlotId { get; set; } = "";
        [JsonPropertyName("staff_id")] public string? StaffId { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "BOOKED";
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    }

    private sealed class SeedAudit
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("actor_id")] public string ActorId { get; set; } = "";
        [JsonPropertyName("actor_role")] public string ActorRole { get; set; } = "";
        [JsonPropertyName("action_type")] public string ActionType { get; set; } = "";
        [JsonPropertyName("entity_type")] public string EntityType { get; set; } = "";
        [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";
        [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; }
        [JsonPropertyName("metadata")] public JsonElement? Metadata { get; set; }
    }

    // Public entry point

    public async Task RunAsync()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "example.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "example.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "seed", "example.json")
        };

        var seedPath = candidates.FirstOrDefault(File.Exists);
        if (seedPath is null)
        {
            logger.LogWarning("SeedImporter: example.json not found — skipping seed.");
            return;
        }

        logger.LogInformation("SeedImporter: loading {Path}", seedPath);

        await using var stream = File.OpenRead(seedPath);
        var seed = await JsonSerializer.DeserializeAsync<SeedFile>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (seed is null)
        {
            logger.LogError("SeedImporter: failed to parse example.json");
            return;
        }

        await SeedBranchesAsync(seed.Branches);
        await SeedUsersAsync(seed.Users);
        await SeedServiceTypesAsync(seed.ServiceTypes);
        await SeedStaffServiceTypesAsync(seed.StaffServiceTypes);
        await SeedSlotsAsync(seed.Slots);
        await SeedAppointmentsAsync(seed.Appointments);
        await SeedAuditLogsAsync(seed.AuditLogs);
        await SeedAppSettingsAsync();

        logger.LogInformation("SeedImporter: completed successfully.");
    }

    // Branches

    private async Task SeedBranchesAsync(List<SeedBranch> items)
    {
        var existing = await db.Branches.Select(b => b.Id).ToHashSetAsync();
        var toAdd = items
            .Where(b => !existing.Contains(b.Id))
            .Select(b => new Branch
            {
                Id = b.Id,
                Name = b.Name,
                City = b.City,
                Address = b.Address,
                Timezone = b.Timezone,
                IsActive = b.IsActive,
                CreatedAt = DateTime.UtcNow
            }).ToList();

        if (toAdd.Count > 0)
        {
            db.Branches.AddRange(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("SeedImporter: inserted {N} branches", toAdd.Count);
        }
    }

    // Users (admin + managers + staff + customers) 

    private async Task SeedUsersAsync(SeedUsers users)
    {
        var all = users.Admin
            .Concat(users.BranchManagers)
            .Concat(users.Staff)
            .Concat(users.Customers)
            .ToList();

        var existing = await db.Users.Select(u => u.Id).ToHashSetAsync();
        var toAdd = all
            .Where(u => !existing.Contains(u.Id))
            .Select(u => new User
            {
                Id = u.Id,
                Username = u.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(u.Password),
                Role = Enum.Parse<UserRole>(u.Role),
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                BranchId = u.BranchId,
                IsActive = u.IsActive,
                CreatedAt = DateTime.UtcNow
            }).ToList();

        if (toAdd.Count > 0)
        {
            db.Users.AddRange(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("SeedImporter: inserted {N} users", toAdd.Count);
        }
    }

    // Service types

    private async Task SeedServiceTypesAsync(List<SeedSvc> items)
    {
        var existing = await db.ServiceTypes.Select(s => s.Id).ToHashSetAsync();
        var toAdd = items
            .Where(s => !existing.Contains(s.Id))
            .Select(s => new ServiceType
            {
                Id = s.Id,
                BranchId = s.BranchId,
                Name = s.Name,
                Description = s.Description,
                DurationMinutes = s.DurationMinutes,
                IsActive = s.IsActive,
                CreatedAt = DateTime.UtcNow
            }).ToList();

        if (toAdd.Count > 0)
        {
            db.ServiceTypes.AddRange(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("SeedImporter: inserted {N} service types", toAdd.Count);
        }
    }

    // Staff -> service type assignments

    private async Task SeedStaffServiceTypesAsync(List<SeedSST> items)
    {
        var existing = await db.StaffServiceTypes
            .Select(x => new { x.StaffId, x.ServiceTypeId })
            .ToListAsync();

        var existingSet = existing
            .Select(x => $"{x.StaffId}:{x.ServiceTypeId}")
            .ToHashSet();

        var toAdd = items
            .Where(x => !existingSet.Contains($"{x.StaffId}:{x.ServiceTypeId}"))
            .Select(x => new StaffServiceType
            {
                StaffId = x.StaffId,
                ServiceTypeId = x.ServiceTypeId,
                AssignedAt = DateTime.UtcNow
            }).ToList();

        if (toAdd.Count > 0)
        {
            db.StaffServiceTypes.AddRange(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("SeedImporter: inserted {N} staff-service assignments", toAdd.Count);
        }
    }

    // Slots

    private async Task SeedSlotsAsync(List<SeedSlot> items)
    {
        var existing = await db.Slots.Select(s => s.Id).ToHashSetAsync();
        var toAdd = items
            .Where(s => !existing.Contains(s.Id))
            .Select(s => new Slot
            {
                Id = s.Id,
                BranchId = s.BranchId,
                ServiceTypeId = s.ServiceTypeId,
                StaffId = s.StaffId,
                StartAt = s.StartAt.ToUniversalTime(),
                EndAt = s.EndAt.ToUniversalTime(),
                Capacity = s.Capacity,
                IsActive = s.IsActive,
                CreatedAt = DateTime.UtcNow
            }).ToList();

        if (toAdd.Count > 0)
        {
            db.Slots.AddRange(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("SeedImporter: inserted {N} slots", toAdd.Count);
        }
    }

    // Appointments

    private async Task SeedAppointmentsAsync(List<SeedAppt> items)
    {
        var existing = await db.Appointments.Select(a => a.Id).ToHashSetAsync();
        var toAdd = items
            .Where(a => !existing.Contains(a.Id))
            .Select(a => new Appointment
            {
                Id = a.Id,
                CustomerId = a.CustomerId,
                BranchId = a.BranchId,
                ServiceTypeId = a.ServiceTypeId,
                SlotId = a.SlotId,
                StaffId = a.StaffId,
                Status = Enum.Parse<AppointmentStatus>(a.Status),
                CreatedAt = a.CreatedAt.ToUniversalTime(),
                UpdatedAt = a.CreatedAt.ToUniversalTime()
            }).ToList();

        if (toAdd.Count > 0)
        {
            db.Appointments.AddRange(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("SeedImporter: inserted {N} appointments", toAdd.Count);
        }
    }

    // Audit logs 

    private async Task SeedAuditLogsAsync(List<SeedAudit> items)
    {
        var existing = await db.AuditLogs.Select(a => a.Id).ToHashSetAsync();
        var toAdd = items
            .Where(a => !existing.Contains(a.Id))
            .Select(a => new AuditLog
            {
                Id = a.Id,
                ActorId = a.ActorId,
                ActorRole = a.ActorRole,
                ActionType = a.ActionType,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Timestamp = a.Timestamp.ToUniversalTime(),
                Metadata = a.Metadata.HasValue
                    ? a.Metadata.Value.GetRawText()
                    : null,
                CreatedAt = a.Timestamp.ToUniversalTime()
            }).ToList();

        if (toAdd.Count > 0)
        {
            db.AuditLogs.AddRange(toAdd);
            await db.SaveChangesAsync();
            logger.LogInformation("SeedImporter: inserted {N} audit logs", toAdd.Count);
        }
    }

    // App settings (retention period default) 

    private async Task SeedAppSettingsAsync()
    {
        var exists = await db.AppSettings
            .AnyAsync(s => s.Key == "soft_delete_retention_days");

        if (!exists)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = "soft_delete_retention_days",
                Value = "30",
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            logger.LogInformation("SeedImporter: inserted default app settings");
        }
    }
}