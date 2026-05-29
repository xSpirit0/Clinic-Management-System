namespace ClinicMVC.Services;

public interface INotificationService
{
    Task SendAsync(
        string? aspNetUserId,
        string notificationTypeName,
        string title,
        string message,
        int? appointmentId = null);
}