using ClinicAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
namespace ClinicAPI.Controllers
{
    [AllowAnonymous]
    [Route("api/public")]
    [ApiController]
    public class PublicLookupController : ControllerBase
    {
        private readonly ClinicDbContext _context;

        public PublicLookupController(ClinicDbContext context)
        {
            _context = context;
        }

        [HttpGet("patient-lookup")]
        public async Task<IActionResult> GetPatientData(string cprNumber, string referenceNumber)
        {
            var patient = await _context.PatientProfiles
                .FirstOrDefaultAsync(p =>
                    p.Cprnumber == cprNumber &&
                    p.PatientReferenceNumber == referenceNumber);

            if (patient == null)
                return NotFound("Patient not found");

            var upcomingAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a =>
                    a.PatientId == patient.PatientId &&
                    a.ScheduledDate >= DateOnly.FromDateTime(DateTime.Today))
                .OrderBy(a => a.ScheduledDate)
                .ToListAsync();

            var recentVisits = await _context.VisitRecords
                .Include(v => v.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(v => v.Appointment)
                .Where(v => v.Appointment.PatientId == patient.PatientId)
                .OrderByDescending(v => v.VisitDate)
                .Take(5)
                .ToListAsync();

            return Ok(new
            {
                PatientId = patient.PatientId,
                UpcomingAppointments = upcomingAppointments,
                RecentVisits = recentVisits
            });
        }
    }
}