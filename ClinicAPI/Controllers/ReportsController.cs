using ClinicAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicAPI.Controllers
{
    // Reports API - all endpoints are ClinicManager only.
    [Authorize(Roles = "ClinicManager")]
    [Route("api/reports")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly ClinicDbContext _context;

        public ReportsController(ClinicDbContext context)
        {
            _context = context;
        }

        // Returns null when the range is valid, a BadRequest result when it isn't.
        // Callers just do: var err = ValidateDateRange(...); if (err != null) return err;
        private IActionResult? ValidateDateRange(DateOnly? from, DateOnly? to)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
                return BadRequest("Invalid date range: 'from' cannot be greater than 'to'.");

            return null;
        }

        // Shared date filter so each endpoint doesn't repeat the same Where clauses.
        private IQueryable<Appointment> ApplyDateFilter(
            IQueryable<Appointment> query,
            DateOnly? from,
            DateOnly? to)
        {
            if (from.HasValue)
                query = query.Where(a => a.ScheduledDate >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.ScheduledDate <= to.Value);

            return query;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            return Ok(new
            {
                totalPatients = await _context.PatientProfiles.CountAsync(),
                totalDoctors = await _context.DoctorProfiles.CountAsync(),
                activeDoctors = await _context.DoctorProfiles.CountAsync(d => d.IsActive),
                totalAppointments = await _context.Appointments.CountAsync(),
                todayAppointments = await _context.Appointments.CountAsync(a => a.ScheduledDate == today),
                upcomingAppointments = await _context.Appointments.CountAsync(a => a.ScheduledDate >= today),
                cancelledAppointments = await _context.Appointments.CountAsync(a => a.AppointmentStatus.AppointmentStatus1 == "Cancelled"),
                missedAppointments = await _context.Appointments.CountAsync(a => a.AppointmentStatus.AppointmentStatus1 == "Missed")
            });
        }

        [HttpGet("appointments/stats")]
        public async Task<IActionResult> GetAppointmentStats(DateOnly? from, DateOnly? to)
        {
            var validation = ValidateDateRange(from, to);
            if (validation != null) return validation;

            var query = ApplyDateFilter(
                _context.Appointments.Include(a => a.AppointmentStatus),
                from,
                to);

            var stats = await query
                .GroupBy(a => a.AppointmentStatus.AppointmentStatus1)
                .Select(g => new
                {
                    status = g.Key,
                    count = g.Count()
                })
                .ToListAsync();

            return Ok(new
            {
                from,
                to,
                totalAppointments = await query.CountAsync(),
                byStatus = stats
            });
        }

        [HttpGet("appointments/cancellation-rate")]
        public async Task<IActionResult> GetCancellationRate(DateOnly? from, DateOnly? to)
        {
            var validation = ValidateDateRange(from, to);
            if (validation != null) return validation;

            var query = ApplyDateFilter(_context.Appointments, from, to);

            var total = await query.CountAsync();
            var cancelled = await query.CountAsync(a =>
                a.AppointmentStatus.AppointmentStatus1 == "Cancelled");

            return Ok(new
            {
                from,
                to,
                totalAppointments = total,
                cancelledAppointments = cancelled,
                // Guard against division by zero when there are no appointments in range.
                cancellationRatePercentage = total == 0
                    ? 0
                    : Math.Round((double)cancelled / total * 100, 2)
            });
        }

        [HttpGet("appointments/missed")]
        public async Task<IActionResult> GetMissedAppointments(DateOnly? from, DateOnly? to)
        {
            var validation = ValidateDateRange(from, to);
            if (validation != null) return validation;

            var query = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.AspNetUser)
                .Include(a => a.Patient).ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "Missed")
                .AsQueryable();

            query = ApplyDateFilter(query, from, to);

            var missed = await query
                .OrderByDescending(a => a.ScheduledDate)
                .Select(a => new
                {
                    appointmentId = a.AppointmentId,
                    scheduledDate = a.ScheduledDate,
                    slotStartTime = a.SlotStartTime,
                    slotEndTime = a.SlotEndTime,
                    doctorName = a.Doctor.AspNetUser != null
                        ? a.Doctor.AspNetUser.FirstName + " " + a.Doctor.AspNetUser.LastName
                        : "Unknown Doctor",
                    patientId = a.PatientId,
                    patientName = a.Patient.AspNetUser != null
                        ? a.Patient.AspNetUser.FirstName + " " + a.Patient.AspNetUser.LastName
                        : "Unknown Patient",
                    specialization = a.Specialization.Name,
                    status = a.AppointmentStatus.AppointmentStatus1
                })
                .ToListAsync();

            return Ok(new
            {
                from,
                to,
                missedAppointments = missed
            });
        }

        [HttpGet("appointments/today")]
        public async Task<IActionResult> GetTodayAppointments()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var appointments = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate == today)
                .OrderBy(a => a.SlotStartTime)
                .Select(a => new
                {
                    appointmentId = a.AppointmentId,
                    scheduledDate = a.ScheduledDate,
                    slotStartTime = a.SlotStartTime,
                    slotEndTime = a.SlotEndTime,
                    doctorName = a.Doctor.AspNetUser != null
                        ? a.Doctor.AspNetUser.FirstName + " " + a.Doctor.AspNetUser.LastName
                        : "Unknown Doctor",
                    patientId = a.PatientId,
                    specialization = a.Specialization.Name,
                    status = a.AppointmentStatus.AppointmentStatus1
                })
                .ToListAsync();

            return Ok(new
            {
                date = today,
                totalAppointments = appointments.Count,
                appointments
            });
        }

        [HttpGet("appointments/upcoming")]
        public async Task<IActionResult> GetUpcomingAppointments(int days = 7)
        {
            // Cap at 365 so someone doesn't accidentally request 10 years of data.
            if (days <= 0 || days > 365)
                return BadRequest("Days must be between 1 and 365.");

            var today = DateOnly.FromDateTime(DateTime.Today);
            var to = today.AddDays(days);

            var appointments = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate >= today && a.ScheduledDate <= to)
                .OrderBy(a => a.ScheduledDate)
                .ThenBy(a => a.SlotStartTime)
                .Select(a => new
                {
                    appointmentId = a.AppointmentId,
                    scheduledDate = a.ScheduledDate,
                    slotStartTime = a.SlotStartTime,
                    slotEndTime = a.SlotEndTime,
                    doctorName = a.Doctor.AspNetUser != null
                        ? a.Doctor.AspNetUser.FirstName + " " + a.Doctor.AspNetUser.LastName
                        : "Unknown Doctor",
                    patientId = a.PatientId,
                    specialization = a.Specialization.Name,
                    status = a.AppointmentStatus.AppointmentStatus1
                })
                .ToListAsync();

            return Ok(new
            {
                from = today,
                to,
                totalAppointments = appointments.Count,
                appointments
            });
        }

        [HttpGet("appointments/busiest-days")]
        public async Task<IActionResult> GetBusiestDays(DateOnly? from, DateOnly? to)
        {
            var validation = ValidateDateRange(from, to);
            if (validation != null) return validation;

            var query = ApplyDateFilter(_context.Appointments, from, to);

            var result = await query
                .GroupBy(a => a.ScheduledDate)
                .Select(g => new
                {
                    date = g.Key,
                    appointmentCount = g.Count()
                })
                .OrderByDescending(x => x.appointmentCount)
                .ThenBy(x => x.date)
                .ToListAsync();

            return Ok(new
            {
                from,
                to,
                busiestDays = result
            });
        }

        // Shows appointment counts for each doctor, so we can see which doctors are busiest and how many appointments they have in different statuses.
        [HttpGet("doctors/utilization")]
        public async Task<IActionResult> GetDoctorUtilization(DateOnly? from, DateOnly? to)
        {
            var validation = ValidateDateRange(from, to);
            if (validation != null) return validation;

            var appointments = ApplyDateFilter(_context.Appointments, from, to);

            var result = await _context.DoctorProfiles
                .Include(d => d.AspNetUser)
                .Select(d => new
                {
                    doctorId = d.DoctorId,
                    doctorName = d.AspNetUser != null
                        ? d.AspNetUser.FirstName + " " + d.AspNetUser.LastName
                        : "Unknown Doctor",
                    totalAppointments = appointments.Count(a => a.DoctorId == d.DoctorId),
                    completedAppointments = appointments.Count(a =>
                        a.DoctorId == d.DoctorId &&
                        a.AppointmentStatus.AppointmentStatus1 == "Completed"),
                    cancelledAppointments = appointments.Count(a =>
                        a.DoctorId == d.DoctorId &&
                        a.AppointmentStatus.AppointmentStatus1 == "Cancelled"),
                    missedAppointments = appointments.Count(a =>
                        a.DoctorId == d.DoctorId &&
                        a.AppointmentStatus.AppointmentStatus1 == "Missed")
                })
                .OrderByDescending(d => d.totalAppointments)
                .ToListAsync();

            return Ok(new
            {
                from,
                to,
                doctors = result
            });
        }


        // Shows appointment counts grouped by specialization, so we can see which specializations are most in demand.
        [HttpGet("specializations/appointments")]
        public async Task<IActionResult> GetAppointmentsBySpecialization(DateOnly? from, DateOnly? to)
        {
            var validation = ValidateDateRange(from, to);
            if (validation != null) return validation;

            var query = _context.Appointments
                .Include(a => a.Specialization)
                .AsQueryable();

            query = ApplyDateFilter(query, from, to);

            var result = await query
                .GroupBy(a => a.Specialization.Name)
                .Select(g => new
                {
                    specialization = g.Key,
                    appointmentCount = g.Count()
                })
                .OrderByDescending(x => x.appointmentCount)
                .ToListAsync();

            return Ok(new
            {
                from,
                to,
                specializations = result
            });
        }
    }
}