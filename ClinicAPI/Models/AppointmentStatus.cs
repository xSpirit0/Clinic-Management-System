using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class AppointmentStatus
{
    public int AppointmentStatusId { get; set; }

    public string AppointmentStatus1 { get; set; } = null!;

    public virtual ICollection<AppointmentStatusHistory> AppointmentStatusHistories { get; set; } = new List<AppointmentStatusHistory>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
