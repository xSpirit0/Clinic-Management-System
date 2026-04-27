using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class AppointmentStatusHistory
{
    public int AppointmentStatusHistoryId { get; set; }

    public int AppointmentId { get; set; }

    public int AppointmentStatusId { get; set; }

    public int ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Notes { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual AppointmentStatus AppointmentStatus { get; set; } = null!;

    public virtual AppUser ChangedByUser { get; set; } = null!;
}
