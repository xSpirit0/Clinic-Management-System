using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class PrescriptionStatus
{
    public int PrescriptionStatusId { get; set; }

    public string PrescriptionStatus1 { get; set; } = null!;

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}
