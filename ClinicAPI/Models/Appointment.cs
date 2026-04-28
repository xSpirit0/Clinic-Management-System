using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class Appointment
{
    public int AppointmentId { get; set; }

    public int PatientId { get; set; }

    public int DoctorId { get; set; }

    public int SpecializationId { get; set; }

    public DateOnly ScheduledDate { get; set; }

    public TimeOnly SlotStartTime { get; set; }

    public TimeOnly SlotEndTime { get; set; }

    public int AppointmentStatusId { get; set; }

    public string? ComplaintReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedByAspNetUserId { get; set; }

    public virtual AppointmentStatus AppointmentStatus { get; set; } = null!;

    public virtual ICollection<AppointmentStatusHistory> AppointmentStatusHistories { get; set; } = new List<AppointmentStatusHistory>();

    public virtual ApplicationUser? CreatedByAspNetUser { get; set; }

    public virtual DoctorProfile Doctor { get; set; } = null!;

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual Specialization Specialization { get; set; } = null!;

    public virtual VisitRecord? VisitRecord { get; set; }
}
