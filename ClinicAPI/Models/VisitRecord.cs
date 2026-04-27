using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class VisitRecord
{
    public int VisitRecordId { get; set; }

    public int AppointmentId { get; set; }

    public int DoctorId { get; set; }

    public string? DoctorNotes { get; set; }

    public string? Diagnosis { get; set; }

    public string? Treatment { get; set; }

    public DateTime VisitDate { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual DoctorProfile Doctor { get; set; } = null!;

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}
