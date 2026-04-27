using ClinicAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicAPI.DTOs;
namespace ClinicAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorsController : ControllerBase
    {
        private readonly ClinicDbContext _context;

        public DoctorsController(ClinicDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetDoctors(int? specializationId)
        {
            var query = _context.DoctorProfiles.AsQueryable();

            if (specializationId.HasValue)
            {
                query = query.Where(d =>
                    d.DoctorSpecializations.Any(ds =>
                        ds.SpecializationId == specializationId.Value));
            }

            var doctors = await query
                .Select(d => new
                {
                    d.DoctorId,
                    Name = d.User.FirstName + " " + d.User.LastName,
                    Specializations = d.DoctorSpecializations
                        .Select(ds => ds.Specialization.Name)
                        .ToList()
                })
                .ToListAsync();

            return Ok(doctors);
        }

        [HttpGet("{id}/available-slots")]
        public async Task<IActionResult> GetAvailableSlots(int id, DateOnly date)
        {
            var dayOfWeek = ((int)date.DayOfWeek) + 1;

            var schedule = await _context.DoctorSchedules
                .FirstOrDefaultAsync(s =>
                    s.DoctorId == id &&
                    s.DayOfWeek == dayOfWeek &&
                    s.IsActive);

            if (schedule == null)
                return NotFound("No active schedule found for this doctor on this date.");

            var bookedSlots = await _context.Appointments
                .Where(a =>
                    a.DoctorId == id &&
                    a.ScheduledDate == date &&
                    a.AppointmentStatusId != 6 &&
                    a.AppointmentStatusId != 7)
                .Select(a => new
                {
                    a.SlotStartTime,
                    a.SlotEndTime
                })
                .ToListAsync();

            var availableSlots = new List<AvailableSlotDto>();

            var current = schedule.StartTime;

            while (current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)) <= schedule.EndTime)
            {
                var slotEnd = current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));

                var isBooked = bookedSlots.Any(b =>
                    current < b.SlotEndTime &&
                    slotEnd > b.SlotStartTime);

                if (!isBooked)
                {
                    availableSlots.Add(new AvailableSlotDto
                    {
                        StartTime = current,
                        EndTime = slotEnd
                    });
                }

                current = slotEnd;
            }

            return Ok(availableSlots);
        }

    }
}