using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class DoctorProfile
{
    public int DoctorId { get; set; }

    public int UserId { get; set; }

    public string LicenseNumber { get; set; } = null!;

    public string? Biography { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<DoctorLeave> DoctorLeaves { get; set; } = new List<DoctorLeave>();

    public virtual ICollection<DoctorSchedule> DoctorSchedules { get; set; } = new List<DoctorSchedule>();

    public virtual ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();

    public virtual AppUser User { get; set; } = null!;

    public virtual ICollection<VisitRecord> VisitRecords { get; set; } = new List<VisitRecord>();
}
