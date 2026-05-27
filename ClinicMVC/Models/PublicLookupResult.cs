namespace ClinicMVC.Models
{
    public class PublicLookupResult
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = "";
        public string PatientReferenceNumber { get; set; } = "";
        public List<LookupAppointmentDto> UpcomingAppointments { get; set; } = new();
        public List<LookupVisitDto> RecentVisits { get; set; } = new();
    }

    public class LookupAppointmentDto
    {
        public int AppointmentId { get; set; }
        public string ScheduledDate { get; set; } = "";
        public string SlotStartTime { get; set; } = "";
        public string SlotEndTime { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string Specialization { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ComplaintReason { get; set; }
    }

    public class LookupVisitDto
    {
        public int VisitRecordId { get; set; }
        public string VisitDate { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string Specialization { get; set; } = "";
        public string? Diagnosis { get; set; }
        public string? Treatment { get; set; }
        public int PrescriptionCount { get; set; }
    }
}