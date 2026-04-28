using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class AppointmentStatusHistory
{
    public int AppointmentStatusHistoryId { get; set; }

    public int AppointmentId { get; set; }

    public int AppointmentStatusId { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Notes { get; set; }

    public string? ChangedByAspNetUserId { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual AppointmentStatus AppointmentStatus { get; set; } = null!;

    public virtual ApplicationUser? ChangedByAspNetUser { get; set; }
}
