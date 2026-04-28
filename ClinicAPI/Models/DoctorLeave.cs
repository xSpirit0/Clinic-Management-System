using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class DoctorLeave
{
    public int DoctorLeaveId { get; set; }

    public int DoctorId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }

    public string? RejectionReason { get; set; }

    public int LeaveStatusId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? ApprovedByAspNetUserId { get; set; }

    public virtual ApplicationUser? ApprovedByAspNetUser { get; set; }

    public virtual DoctorProfile Doctor { get; set; } = null!;

    public virtual LeaveStatus LeaveStatus { get; set; } = null!;
}
