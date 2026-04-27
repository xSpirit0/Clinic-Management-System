using ClinicAPI.DTOs;
using ClinicAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClinicAPI.DTOs;
namespace ClinicAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly ClinicDbContext _context;

        public AppointmentsController(ClinicDbContext context)
        {
            _context = context;
        }

        // GET: api/Appointments
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AppointmentDto>>> GetAppointments()
        {
            var appointments = await _context.Appointments
         .Select(a => new AppointmentDto
             {
                AppointmentId = a.AppointmentId,
                ScheduledDate = a.ScheduledDate,
                SlotStartTime = a.SlotStartTime,
                SlotEndTime = a.SlotEndTime,

                PatientName = a.Patient.User.FirstName + " " + a.Patient.User.LastName,
                DoctorName = a.Doctor.User.FirstName + " " + a.Doctor.User.LastName,

                Specialization = a.Specialization.Name,
                Status = a.AppointmentStatus.AppointmentStatus1
            })
            .ToListAsync();

            return Ok(appointments);
        }

        // GET: api/Appointments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AppointmentDto>> GetAppointment(int id)
        {
            var appointment = await _context.Appointments
                .Where(a => a.AppointmentId == id)
                .Select(a => new AppointmentDto
                {
                    AppointmentId = a.AppointmentId,
                    PatientName = a.Patient.User.FirstName + " " + a.Patient.User.LastName,
                    DoctorName = a.Doctor.User.FirstName + " " + a.Doctor.User.LastName,
                    Specialization = a.Specialization.Name,
                    ScheduledDate = a.ScheduledDate,
                    SlotStartTime = a.SlotStartTime,
                    SlotEndTime = a.SlotEndTime,
                    Status = a.AppointmentStatus.AppointmentStatus1
                })
                .FirstOrDefaultAsync();

            if (appointment == null)
            {
                return NotFound();
            }

            return Ok(appointment);
        }

        // PUT: api/Appointments/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAppointment(int id, Appointment appointment)
        {
            if (id != appointment.AppointmentId)
            {
                return BadRequest();
            }

            _context.Entry(appointment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AppointmentExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Appointments
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Appointment>> PostAppointment(CreateAppointmentDto dto)
        {
            var hasConflict = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == dto.DoctorId &&
                a.ScheduledDate == dto.ScheduledDate &&
                a.AppointmentStatusId != 6 &&
                a.AppointmentStatusId != 7 &&
                dto.SlotStartTime < a.SlotEndTime &&
                dto.SlotEndTime > a.SlotStartTime
            );

            if (hasConflict)
            {
                return BadRequest("This doctor already has an appointment during this time");
            }

            var appointment = new Appointment
            {
                PatientId = dto.PatientId,
                DoctorId = dto.DoctorId,
                SpecializationId = dto.SpecializationId,
                ScheduledDate = dto.ScheduledDate,
                SlotStartTime = dto.SlotStartTime,
                SlotEndTime = dto.SlotEndTime,
                ComplaintReason = dto.ComplaintReason,
                CreatedByUserId = dto.CreatedByUserId,
                AppointmentStatusId = 1
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

             var result = await _context.Appointments
            .Where(a => a.AppointmentId == appointment.AppointmentId)
            .Select(a => new AppointmentDto
            {
                AppointmentId = a.AppointmentId,
                PatientName = a.Patient.User.FirstName + " " + a.Patient.User.LastName,
                DoctorName = a.Doctor.User.FirstName + " " + a.Doctor.User.LastName,
                Specialization = a.Specialization.Name,
                ScheduledDate = a.ScheduledDate,
                SlotStartTime = a.SlotStartTime,
                SlotEndTime = a.SlotEndTime,
                Status = a.AppointmentStatus.AppointmentStatus1
            })
            .FirstAsync();
            return CreatedAtAction(nameof(GetAppointment), new { id = result.AppointmentId }, result);
        }

        // DELETE: api/Appointments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAppointment(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.AppointmentId == id);
        }
    }
}
