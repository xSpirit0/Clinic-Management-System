namespace ClinicAPI.DTOs
{
    public class UpdateAppointmentStatusDto
    {
        public int AppointmentStatusId { get; set; }
        public int ChangedByUserId { get; set; }
        public string? Notes { get; set; }
    }
}
