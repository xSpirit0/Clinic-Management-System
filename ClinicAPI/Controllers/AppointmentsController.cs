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
        public async Task<IActionResult> PutAppointment(int id, UpdateAppointmentDto dto)
        {
            var appointment = await _context.Appointments.FindAsync(id);

            if (appointment == null)
                return NotFound();

            if (appointment.AppointmentStatusId == 6 || appointment.AppointmentStatusId == 7)
                return BadRequest("Cannot update a completed or cancelled appointment.");

            var hasConflict = await _context.Appointments.AnyAsync(a =>
                a.AppointmentId != id &&
                a.DoctorId == dto.DoctorId &&
                a.ScheduledDate == dto.ScheduledDate &&
                a.AppointmentStatusId != 6 &&
                a.AppointmentStatusId != 7 &&
                dto.SlotStartTime < a.SlotEndTime &&
                dto.SlotEndTime > a.SlotStartTime
            );

            if (hasConflict)
                return BadRequest("This doctor already has an appointment during this time.");

            appointment.DoctorId = dto.DoctorId;
            appointment.SpecializationId = dto.SpecializationId;
            appointment.ScheduledDate = dto.ScheduledDate;
            appointment.SlotStartTime = dto.SlotStartTime;
            appointment.SlotEndTime = dto.SlotEndTime;
            appointment.ComplaintReason = dto.ComplaintReason;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Appointments
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<AppointmentDto>> PostAppointment(CreateAppointmentDto dto)
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

        [HttpPut("{id}/status")]
        public async Task<ActionResult<AppointmentDto>> UpdateAppointmentStatus(int id, UpdateAppointmentStatusDto dto)
        {
            var appointment = await _context.Appointments.FindAsync(id);

            if (appointment == null)
                return NotFound();

            var currentStatus = appointment.AppointmentStatusId;
            var newStatus = dto.AppointmentStatusId;

            var isValidTransition =
                (currentStatus == 1 && (newStatus == 2 || newStatus == 6)) || // requested -> confirmed/cancelled
                (currentStatus == 2 && (newStatus == 3 || newStatus == 6 || newStatus == 7)) || // confirmed -> checkedIn/cancelled/missed
                (currentStatus == 3 && newStatus == 4) || // checkedIn -> inProgress
                (currentStatus == 4 && newStatus == 5);   // inProgress -> completed

            if (!isValidTransition)
                return BadRequest("Invalid appointment status transition.");

            appointment.AppointmentStatusId = newStatus;
            appointment.UpdatedAt = DateTime.Now;

            var history = new AppointmentStatusHistory
            {
                AppointmentId = id,
                AppointmentStatusId = newStatus,
                ChangedByUserId = dto.ChangedByUserId,
                Notes = dto.Notes
            };

            _context.AppointmentStatusHistories.Add(history);
            await _context.SaveChangesAsync();

            var result = await _context.Appointments
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
                .FirstAsync();

            return Ok(result);
        }
        // DELETE: api/Appointments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAppointment(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);

            if (appointment == null)
                return NotFound();

            if (appointment.AppointmentStatusId == 5)
                return BadRequest("Cannot cancel a completed appointment.");

            if (appointment.AppointmentStatusId == 6)
                return BadRequest("Appointment is already cancelled.");

            appointment.AppointmentStatusId = 6;
            appointment.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("patient/{patientId}")]
        public async Task<IActionResult> GetPatientAppointments(int patientId)
        {
            var appointments = await _context.Appointments
                .Where(a => a.PatientId == patientId)
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
                .ToListAsync();

            return Ok(appointments);
        }

        [HttpGet("doctor/{doctorId}")]
        public async Task<IActionResult> GetDoctorAppointments(
          int doctorId,
          DateOnly? date,
          int? statusId)
        {
            var query = _context.Appointments
                .Where(a => a.DoctorId == doctorId);

            if (date.HasValue)
            {
                query = query.Where(a => a.ScheduledDate == date.Value);
            }

            if (statusId.HasValue)
            {
                query = query.Where(a => a.AppointmentStatusId == statusId.Value);
            }

            var appointments = await query
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
                .ToListAsync();

            return Ok(appointments);
        }
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var totalAppointments = await _context.Appointments.CountAsync();

            var todayAppointments = await _context.Appointments
                .CountAsync(a => a.ScheduledDate == today);

            var completedAppointments = await _context.Appointments
                .CountAsync(a => a.AppointmentStatusId == 5); // completed

            var cancelledAppointments = await _context.Appointments
                .CountAsync(a => a.AppointmentStatusId == 7); // cancelled

            return Ok(new
            {
                totalAppointments,
                todayAppointments,
                completedAppointments,
                cancelledAppointments
            });
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.AppointmentId == id);
        }
    }
}
