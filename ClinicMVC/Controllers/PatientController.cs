using ClinicAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    public class PatientController : Controller
    {
        private readonly ClinicDbContext _context;

        public PatientController(ClinicDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            int tempPatientId = 1;

            var upcomingAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.PatientId == tempPatientId &&
                       (a.AppointmentStatus.AppointmentStatus1 == "Requested" ||
                        a.AppointmentStatus.AppointmentStatus1 == "Confirmed") &&
                       a.ScheduledDate >= DateOnly.FromDateTime(DateTime.Today))
                .OrderBy(a => a.ScheduledDate)
                .Take(5)
                .ToListAsync();

            ViewBag.UpcomingAppointments = upcomingAppointments;
            return View();
        }

        public async Task<IActionResult> MyAppointments()
        {
            int tempPatientId = 1;

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.PatientId == tempPatientId)
                .OrderByDescending(a => a.ScheduledDate)
                .ToListAsync();

            return View(appointments);
        }

        public async Task<IActionResult> AppointmentDetails(int id)
        {
            int tempPatientId = 1;

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                    .ThenInclude(v => v.Prescriptions)
                        .ThenInclude(p => p.PrescriptionItems)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                     a.PatientId == tempPatientId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            return View(appointment);
        }

        public async Task<IActionResult> CancelAppointment(int id)
        {
            int tempPatientId = 1;

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                     a.PatientId == tempPatientId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            if (appointment.AppointmentStatus.AppointmentStatus1 != "Requested" &&
                appointment.AppointmentStatus.AppointmentStatus1 != "Confirmed")
            {
                TempData["Error"] = "This appointment cannot be cancelled.";
                return RedirectToAction("MyAppointments");
            }

            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointmentConfirmed(int id, string reason)
        {
            int tempPatientId = 1;

            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                     a.PatientId == tempPatientId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            var cancelledStatus = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "Cancelled");

            if (cancelledStatus == null)
            {
                TempData["Error"] = "System error: Cancelled status not configured.";
                return RedirectToAction("MyAppointments");
            }

            appointment.AppointmentStatusId = cancelledStatus.AppointmentStatusId;
            appointment.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Appointment cancelled successfully.";
            return RedirectToAction("MyAppointments");
        }

        public async Task<IActionResult> MedicalHistory()
        {
            int tempPatientId = 1;

            var visitRecords = await _context.VisitRecords
                .Include(v => v.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(v => v.Appointment)
                    .ThenInclude(a => a.Specialization)
                .Where(v => v.Appointment.PatientId == tempPatientId)
                .OrderByDescending(v => v.VisitDate)
                .ToListAsync();

            return View(visitRecords);
        }

        public async Task<IActionResult> VisitRecordDetails(int id)
        {
            int tempPatientId = 1;

            var visitRecord = await _context.VisitRecords
                .Include(v => v.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(v => v.Appointment)
                    .ThenInclude(a => a.Specialization)
                .Include(v => v.Prescriptions)
                    .ThenInclude(p => p.PrescriptionItems)
                .FirstOrDefaultAsync(v => v.VisitRecordId == id &&
                                     v.Appointment.PatientId == tempPatientId);

            if (visitRecord == null)
            {
                TempData["Error"] = "Record not found.";
                return RedirectToAction("MedicalHistory");
            }

            return View(visitRecord);
        }

        public async Task<IActionResult> MyPrescriptions()
        {
            int tempPatientId = 1;

            var prescriptions = await _context.Prescriptions
                .Include(p => p.VisitRecord)
                    .ThenInclude(v => v.Doctor)
                        .ThenInclude(d => d.AspNetUser)
                .Include(p => p.VisitRecord)
                    .ThenInclude(v => v.Appointment)
                .Include(p => p.PrescriptionItems)
                .Include(p => p.PrescriptionStatus)
                .Where(p => p.VisitRecord.Appointment.PatientId == tempPatientId)
                .OrderByDescending(p => p.IssuedAt)
                .ToListAsync();

            return View(prescriptions);
        }

        public async Task<IActionResult> PrescriptionDetails(int id)
        {
            int tempPatientId = 1;

            var prescription = await _context.Prescriptions
                .Include(p => p.VisitRecord)
                    .ThenInclude(v => v.Doctor)
                        .ThenInclude(d => d.AspNetUser)
                .Include(p => p.VisitRecord)
                    .ThenInclude(v => v.Appointment)
                .Include(p => p.PrescriptionItems)
                .Include(p => p.PrescriptionStatus)
                .FirstOrDefaultAsync(p => p.PrescriptionId == id &&
                                     p.VisitRecord.Appointment.PatientId == tempPatientId);

            if (prescription == null)
            {
                TempData["Error"] = "Prescription not found.";
                return RedirectToAction("MyPrescriptions");
            }

            return View(prescription);
        }

        public async Task<IActionResult> Notifications()
        {
            var notifications = new List<Notification>();
            return View(notifications);
        }

        public async Task<IActionResult> BookAppointment()
        {
            var specializations = await _context.Specializations
                .Where(s => _context.DoctorSpecializations
                    .Any(ds => ds.SpecializationId == s.SpecializationId &&
                               ds.Doctor.IsActive))
                .ToListAsync();

            ViewBag.Specializations = specializations;
            return View();
        }

        public async Task<IActionResult> GetDoctorsBySpecialization(int specializationId)
        {
            var doctors = await _context.DoctorSpecializations
                .Include(ds => ds.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Where(ds => ds.SpecializationId == specializationId &&
                             ds.Doctor.IsActive)
                .Select(ds => new
                {
                    id = ds.Doctor.DoctorId,
                    name = ds.Doctor.AspNetUser != null
                        ? ds.Doctor.AspNetUser.FirstName
                        : "Unknown"
                })
                .ToListAsync();

            return Json(doctors);
        }

        public async Task<IActionResult> GetAvailableSlots(int doctorId, DateOnly date)
        {
            int dayOfWeek = (int)date.DayOfWeek;
            var schedule = await _context.DoctorSchedules
                .FirstOrDefaultAsync(s => s.DoctorId == doctorId &&
                                     s.DayOfWeek == dayOfWeek &&
                                     s.IsActive);

            if (schedule == null)
                return Json(new List<object>());

            var approvedLeaveStatusId = await _context.LeaveStatuses
                .Where(ls => ls.LeaveStatus1 == "Approved")
                .Select(ls => ls.LeaveStatusId)
                .FirstOrDefaultAsync();

            var isOnLeave = await _context.DoctorLeaves
                .AnyAsync(l => l.DoctorId == doctorId &&
                          l.StartDate <= date &&
                          l.EndDate >= date &&
                          l.LeaveStatusId == approvedLeaveStatusId);

            if (isOnLeave)
                return Json(new List<object>());

            var slots = new List<object>();
            var current = schedule.StartTime;

            while (current.Add(TimeSpan.FromMinutes(
                schedule.SlotDurationMinutes)) <= schedule.EndTime)
            {
                var slotEnd = current.Add(
                    TimeSpan.FromMinutes(schedule.SlotDurationMinutes));

                var isBooked = await _context.Appointments
                    .Include(a => a.AppointmentStatus)
                    .AnyAsync(a => a.DoctorId == doctorId &&
                             a.ScheduledDate == date &&
                             a.SlotStartTime == current &&
                             a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                             a.AppointmentStatus.AppointmentStatus1 != "Missed");

                if (!isBooked)
                {
                    slots.Add(new
                    {
                        startTime = current.ToString(),
                        endTime = slotEnd.ToString(),
                        display = current.ToString("hh\\:mm") +
                                 " - " + slotEnd.ToString("hh\\:mm")
                    });
                }

                current = slotEnd;
            }

            return Json(slots);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(
            int specializationId, int doctorId,
            DateOnly scheduledDate,
            TimeOnly slotStartTime, TimeOnly slotEndTime)
        {
            int tempPatientId = 1;
            string? tempCreatedByAspNetUserId = null;

            var isBooked = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .AnyAsync(a => a.DoctorId == doctorId &&
                         a.ScheduledDate == scheduledDate &&
                         a.SlotStartTime == slotStartTime &&
                         a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                         a.AppointmentStatus.AppointmentStatus1 != "Missed");

            if (isBooked)
            {
                TempData["Error"] = "This slot is no longer available.";
                return RedirectToAction("BookAppointment");
            }

            var requestedStatus = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "Requested");

            if (requestedStatus == null)
            {
                TempData["Error"] = "System error: Requested status not configured.";
                return RedirectToAction("BookAppointment");
            }

            var appointment = new Appointment
            {
                PatientId = tempPatientId,
                DoctorId = doctorId,
                SpecializationId = specializationId,
                ScheduledDate = scheduledDate,
                SlotStartTime = slotStartTime,
                SlotEndTime = slotEndTime,
                AppointmentStatusId = requestedStatus.AppointmentStatusId,
                CreatedByAspNetUserId = tempCreatedByAspNetUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Appointment booked successfully!";
            return RedirectToAction("MyAppointments");
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }
    }
}