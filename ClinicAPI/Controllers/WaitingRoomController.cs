using ClinicAPI.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ClinicAPI.Controllers
{
    // This controller is protected by the "ClinicManagerOnly" policy, which means only users with the "ClinicManager" role can access its endpoints.
    [ApiController]
    [Route("api/waitingroom")]
    // This controller is responsible for notifying connected board displays when the waiting room data has changed.
    public class WaitingRoomController : ControllerBase
    {
        private readonly IHubContext<WaitingRoomHub> _hub;

        public WaitingRoomController(IHubContext<WaitingRoomHub> hub)
        {
            _hub = hub;
        }

        // Called by MVC after an appointment status changes.
        // Broadcasts "WaitingRoomUpdated" to all connected board displays
        // so each one reloads with fresh data.
        [HttpPost("notify-update")]
        public async Task<IActionResult> NotifyUpdate()
        {
            await _hub.Clients.All.SendAsync("WaitingRoomUpdated");
            return Ok();
        }
    }
}