using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class LeaveStatus
{
    public int LeaveStatusId { get; set; }

    public string LeaveStatus1 { get; set; } = null!;

    public virtual ICollection<DoctorLeave> DoctorLeaves { get; set; } = new List<DoctorLeave>();
}
