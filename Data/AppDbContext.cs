using AppointmentBookingSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Xml;
using static System.Reflection.Metadata.BlobBuilder;

namespace AppointmentBookingSystem.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // DbSets
    public DbSet<User> Users => Set<User>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<StaffServiceType> StaffServiceTypes => Set<StaffServiceType>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Branch
        b.Entity<Branch>(e =>
        {
            e.HasKey(x => x.Id);
        });

        // User
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasConversion<string>();
            e.HasOne(x => x.Branch)
             .WithMany(x => x.Staff)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ServiceType
        b.Entity<ServiceType>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Branch)
             .WithMany(x => x.ServiceTypes)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // StaffServiceType (composite PK)
        b.Entity<StaffServiceType>(e =>
        {
            e.HasKey(x => new { x.StaffId, x.ServiceTypeId });
            e.HasOne(x => x.Staff)
             .WithMany(x => x.StaffServiceTypes)
             .HasForeignKey(x => x.StaffId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ServiceType)
             .WithMany(x => x.StaffServiceTypes)
             .HasForeignKey(x => x.ServiceTypeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Slot
        b.Entity<Slot>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Branch)
             .WithMany(x => x.Slots)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ServiceType)
             .WithMany(x => x.Slots)
             .HasForeignKey(x => x.ServiceTypeId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Staff)
             .WithMany()
             .HasForeignKey(x => x.StaffId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // Appointment
        b.Entity<Appointment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.SlotId);
            e.HasIndex(x => x.CustomerId);
            e.HasOne(x => x.Customer)
             .WithMany(x => x.Appointments)
             .HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServiceType)
             .WithMany()
             .HasForeignKey(x => x.ServiceTypeId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Slot)
             .WithMany(x => x.Appointments)
             .HasForeignKey(x => x.SlotId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Staff)
             .WithMany()
             .HasForeignKey(x => x.StaffId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // AuditLog
        b.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.BranchId);
            e.HasIndex(x => x.Timestamp);
        });

        // AppSetting 
        b.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
        });
    }
}
