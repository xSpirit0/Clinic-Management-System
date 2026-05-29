using ClinicAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Services;

public class NotificationService : INotificationService
{
    private readonly ClinicDbContext _context;

    public NotificationService(ClinicDbContext context)
    {
        _context = context;
    }

    public async Task SendAsync(
        string? aspNetUserId,
        string notificationTypeName,
        string title,
        string message,
        int? appointmentId = null)
    {
        if (string.IsNullOrEmpty(aspNetUserId)) return;

        var type = await _context.NotificationTypes
            .FirstOrDefaultAsync(t => t.Type == notificationTypeName);

        if (type == null)
        {
            type = new NotificationType { Type = notificationTypeName };
            _context.NotificationTypes.Add(type);
            await _context.SaveChangesAsync();
        }

        _context.Notifications.Add(new Notification
        {
            AspNetUserId = aspNetUserId,
            NotificationTypeId = type.NotificationTypeId,
            AppointmentId = appointmentId,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync();
    }
}