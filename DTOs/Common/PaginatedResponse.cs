namespace AppointmentBookingSystem.DTOs.Common
{
    public record PaginatedResponse<T>(
    IEnumerable<T> Results,
    int Total);
}
