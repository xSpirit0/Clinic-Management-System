namespace ClinicMVC.Services;

// This interface defines a contract for a notification service that can send notifications to users. The SendAsync method takes parameters for the user ID, notification type, title, message, and an optional appointment ID. Implementations of this interface can use various methods (e.g., email, SMS, in-app notifications) to deliver the notifications to the intended recipients.
public interface INotificationService
{
    Task SendAsync(
        string? aspNetUserId,
        string notificationTypeName,
        string title,
        string message,
        int? appointmentId = null);
}