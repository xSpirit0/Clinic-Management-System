using ClinicAPI.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ClinicAPI.Controllers
{
    [ApiController]
    [Route("api/waitingroom")]
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