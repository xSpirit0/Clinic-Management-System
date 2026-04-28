using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class PatientProfile
{
    public int PatientId { get; set; }

    public string Cprnumber { get; set; } = null!;

    public string PatientReferenceNumber { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? BloodType { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? AspNetUserId { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ApplicationUser? AspNetUser { get; set; }
}
