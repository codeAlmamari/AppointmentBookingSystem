using AppointmentBookingSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentBookingSystem.DTOs.Appointment;

public record BookAppointmentRequest(
    [property: FromForm(Name = "slot_id")] string SlotId,
    [property: FromForm(Name = "attachment")] IFormFile? Attachment);

public record RescheduleRequest(
    string NewSlotId);

public record UpdateStatusRequest(
    AppointmentStatus Status,
    string? Notes);

public record AppointmentSummaryResponse(
    string Id,
    string BranchId,
    string ServiceTypeId,
    string SlotId,
    string? StaffId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CustomerBriefResponse Customer,
    SlotBriefResponse Slot,
    ServiceBriefResponse Service);

public record AppointmentDetailResponse(
    string Id,
    string BranchId,
    string ServiceTypeId,
    string SlotId,
    string? StaffId,
    string Status,
    string? Notes,
    bool HasAttachment,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CustomerBriefResponse Customer,
    SlotBriefResponse Slot,
    ServiceBriefResponse Service);

// Nested brief responses 
public record CustomerBriefResponse(
    string Id,
    string FullName,
    string Email);

public record SlotBriefResponse(
    DateTime StartAt,
    DateTime EndAt);

public record ServiceBriefResponse(
    string Name);