using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class Prescription
{
    public int PrescriptionId { get; set; }

    public int VisitRecordId { get; set; }

    public int PrescriptionStatusId { get; set; }

    public DateTime IssuedAt { get; set; }

    public virtual ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();

    public virtual PrescriptionStatus PrescriptionStatus { get; set; } = null!;

    public virtual VisitRecord VisitRecord { get; set; } = null!;
}
