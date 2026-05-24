using Microsoft.AspNetCore.SignalR;

namespace ClinicAPI.Hubs
{
    public class WaitingRoomHub : Hub
    {
        // Public-style hub for the Waiting Room TV display.
        // Clients only listen; server broadcasts from API endpoints via IHubContext.
        // No PII in broadcasts — payload is just a "refresh" signal.
    }
}