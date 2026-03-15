namespace AppointmentBookingSystem.Models
{
    public class AppSetting
    {
        public string Key { get; set; } = null!;
        public string Value { get; set; } = null!;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
