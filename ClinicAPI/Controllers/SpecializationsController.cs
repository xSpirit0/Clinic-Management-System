using ClinicAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpecializationsController : ControllerBase
    {
        private readonly ClinicDbContext _context;

        public SpecializationsController(ClinicDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetSpecializations()
        {
            var list = await _context.Specializations
                .Select(s => new
                {
                    s.SpecializationId,
                    s.Name
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}
