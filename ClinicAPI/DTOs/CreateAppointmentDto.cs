namespace ClinicAPI.DTOs
{
    public class CreateAppointmentDto
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public int SpecializationId { get; set; }
        public DateOnly ScheduledDate { get; set; }
        public TimeOnly SlotStartTime { get; set; }
        public TimeOnly SlotEndTime { get; set; }
        public string? ComplaintReason { get; set; }
        public int CreatedByUserId { get; set; }
    }
}
