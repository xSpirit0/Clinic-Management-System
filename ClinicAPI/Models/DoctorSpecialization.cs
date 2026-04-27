using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class DoctorSpecialization
{
    public int DoctorSpecializationId { get; set; }

    public int DoctorId { get; set; }

    public int SpecializationId { get; set; }

    public virtual DoctorProfile Doctor { get; set; } = null!;

    public virtual Specialization Specialization { get; set; } = null!;
}
