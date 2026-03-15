namespace AppointmentBookingSystem.Models
{
    public enum UserRole {
        ADMIN, 
        BRANCH_MANAGER,
        STAFF,
        CUSTOMER
    }

    public enum AppointmentStatus { 
        BOOKED,
        CHECKED_IN,
        NO_SHOW, COMPLETED,
        CANCELLED 
    }
}
