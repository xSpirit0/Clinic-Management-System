using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class NotificationType
{
    public int NotificationTypeId { get; set; }

    public string Type { get; set; } = null!;

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
