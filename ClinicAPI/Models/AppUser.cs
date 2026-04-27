using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class AppUser
{
    public int UserId { get; set; }

    public string? AspNetUserId { get; set; }

    public int UserRoleId { get; set; }

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AppointmentStatusHistory> AppointmentStatusHistories { get; set; } = new List<AppointmentStatusHistory>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<DoctorLeave> DoctorLeaves { get; set; } = new List<DoctorLeave>();

    public virtual DoctorProfile? DoctorProfile { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual PatientProfile? PatientProfile { get; set; }

    public virtual UserRole UserRole { get; set; } = null!;
}
