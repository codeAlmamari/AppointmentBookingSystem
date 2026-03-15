namespace AppointmentBookingSystem.Models
{
    public class Branch : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string City { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string Timezone { get; set; } = "Muscat";
        public bool IsActive { get; set; } = true;

        public ICollection<ServiceType> ServiceTypes { get; set; } = [];
        public ICollection<Slot> Slots { get; set; } = [];
        public ICollection<User> Staff { get; set; } = [];
    }
}
