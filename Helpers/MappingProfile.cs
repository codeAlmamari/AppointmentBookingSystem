using AppointmentBookingSystem.DTOs.Appointment;
using AppointmentBookingSystem.DTOs.AuditLog;
using AppointmentBookingSystem.DTOs.Branch;
using AppointmentBookingSystem.DTOs.Customer;
using AppointmentBookingSystem.DTOs.ServiceType;
using AppointmentBookingSystem.DTOs.Slot;
using AppointmentBookingSystem.DTOs.Staff;
using AppointmentBookingSystem.Models;
using AutoMapper;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AppointmentBookingSystem.Helpers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Branch 
        CreateMap<Branch, BranchResponse>();

        // ServiceType 
        CreateMap<ServiceType, ServiceTypeResponse>();

        // Slot 
        CreateMap<Slot, SlotResponse>()
            .ForMember(dest => dest.Booked, opt => opt.MapFrom(src =>
                src.Appointments.Count(a => a.Status != AppointmentStatus.CANCELLED)));

        // Staff 
        CreateMap<User, StaffResponse>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));

        // Customer 
        CreateMap<User, CustomerResponse>()
            .ForMember(dest => dest.HasIdImage, opt => opt.MapFrom(src =>
                src.IdImagePath != null));

        // Appointment 
        CreateMap<Appointment, AppointmentSummaryResponse>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                src.Status.ToString()))
            .ForMember(dest => dest.Customer, opt => opt.MapFrom(src =>
                src.Customer))
            .ForMember(dest => dest.Slot, opt => opt.MapFrom(src =>
                src.Slot))
            .ForMember(dest => dest.Service, opt => opt.MapFrom(src =>
                src.ServiceType));

        CreateMap<Appointment, AppointmentDetailResponse>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                src.Status.ToString()))
            .ForMember(dest => dest.HasAttachment, opt => opt.MapFrom(src =>
                src.AttachmentPath != null))
            .ForMember(dest => dest.Customer, opt => opt.MapFrom(src =>
                src.Customer))
            .ForMember(dest => dest.Slot, opt => opt.MapFrom(src =>
                src.Slot))
            .ForMember(dest => dest.Service, opt => opt.MapFrom(src =>
                src.ServiceType));

        // Nested brief responses 
        CreateMap<User, CustomerBriefResponse>()
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email));

        CreateMap<Slot, SlotBriefResponse>();

        CreateMap<ServiceType, ServiceBriefResponse>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));

        // AuditLog 
        CreateMap<AuditLog, AuditLogResponse>();
    }
}