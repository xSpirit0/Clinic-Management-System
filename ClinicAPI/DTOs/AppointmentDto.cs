public class AppointmentDto
{
    public int AppointmentId { get; set; }
    public string PatientName { get; set; }
    public string DoctorName { get; set; }
    public string Specialization { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public TimeOnly SlotStartTime { get; set; }
    public TimeOnly SlotEndTime { get; set; }
    public string Status { get; set; }
}