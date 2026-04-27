using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public int NotificationTypeId { get; set; }

    public int? AppointmentId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual NotificationType NotificationType { get; set; } = null!;

    public virtual AppUser User { get; set; } = null!;
}
