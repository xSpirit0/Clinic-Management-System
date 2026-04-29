namespace ClinicReporting.Models
{
    public class SummaryReport
    {
        public int TotalPatients { get; set; }
        public int TotalDoctors { get; set; }
        public int ActiveDoctors { get; set; }
        public int TotalAppointments { get; set; }
        public int TodayAppointments { get; set; }
        public int UpcomingAppointments { get; set; }
        public int CancelledAppointments { get; set; }
        public int MissedAppointments { get; set; }
    }

    public class AppointmentStatItem
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
    }

    public class AppointmentStatsReport
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public int TotalAppointments { get; set; }
        public List<AppointmentStatItem> ByStatus { get; set; } = new();
    }

    public class CancellationRateReport
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public int TotalAppointments { get; set; }
        public int CancelledAppointments { get; set; }
        public double CancellationRatePercentage { get; set; }
    }

    public class DoctorUtilizationItem
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
        public int TotalAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int CancelledAppointments { get; set; }
        public int MissedAppointments { get; set; }
    }

    public class DoctorUtilizationReport
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public List<DoctorUtilizationItem> Doctors { get; set; } = new();
    }

    public class SpecializationAppointmentItem
    {
        public string Specialization { get; set; } = "";
        public int AppointmentCount { get; set; }
    }

    public class SpecializationReport
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public List<SpecializationAppointmentItem> Specializations { get; set; } = new();
    }
}