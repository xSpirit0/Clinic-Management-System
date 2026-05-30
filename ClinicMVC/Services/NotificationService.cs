using ClinicAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Services;

public class NotificationService : INotificationService
{
    // This service is responsible for managing notifications in the clinic application. It interacts with the ClinicDbContext to create and store notifications in the database. The service ensures that notification types are created if they do not already exist, and it allows for sending notifications to users based on their ASP.NET user ID.
    private readonly ClinicDbContext _context;

    // Constructor to inject the ClinicDbContext dependency, which is used to interact with the database for managing notifications and notification types.
    public NotificationService(ClinicDbContext context)
    {
        _context = context;
    }

    // This method creates a new notification for a user. It checks if the notification type exists, and if not, it creates it. Then it adds the notification to the database and saves the changes.
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