using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class Specialization
{
    public int SpecializationId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();
}
