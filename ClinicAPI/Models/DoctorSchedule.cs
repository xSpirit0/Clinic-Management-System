using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class DoctorSchedule
{
    public int ScheduleId { get; set; }

    public int DoctorId { get; set; }

    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int SlotDurationMinutes { get; set; }

    public bool IsActive { get; set; }

    public virtual DoctorProfile Doctor { get; set; } = null!;
}
