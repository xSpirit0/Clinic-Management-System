using ClinicAPI.Models;
using ClinicMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    [Authorize(Roles = "Patient")]
    public class PatientController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public PatientController(
            ClinicDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // Maps the logged-in Identity user to their PatientProfile. Everything in here
        // keys off PatientId, not the AspNetUser id, so I look it up once per request.
        // Returns null if there's no profile row - the actions handle that by bouncing to login.
        private async Task<PatientProfile?> GetCurrentPatientAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            return await _context.PatientProfiles
                .FirstOrDefaultAsync(p => p.AspNetUserId == user.Id);
        }

        public async Task<IActionResult> Dashboard()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                // Logged in but no profile - shouldn't normally happen, so send them back to login.
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // Only the next few appointments that still need attention (not past, not finished).
            var upcomingAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.PatientId == patient.PatientId &&
                       (a.AppointmentStatus.AppointmentStatus1 == "Requested" ||
                        a.AppointmentStatus.AppointmentStatus1 == "Confirmed") &&
                       a.ScheduledDate >= DateOnly.FromDateTime(DateTime.Today))
                .OrderBy(a => a.ScheduledDate)
                .Take(5)
                .ToListAsync();

            // Drives the little number on the notification bell in the navbar.
            ViewBag.UnreadNotifications = await _context.Notifications
                .CountAsync(n => n.AspNetUserId == patient.AspNetUserId && !n.IsRead);

            ViewBag.UpcomingAppointments = upcomingAppointments;
            return View();
        }

        public async Task<IActionResult> MyAppointments()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.PatientId == patient.PatientId)
                .OrderByDescending(a => a.ScheduledDate)
                .ToListAsync();

            return View(appointments);
        }

        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // The PatientId check in the Where is the important bit - stops someone from
            // opening another patient's appointment by guessing the id in the URL.
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                    .ThenInclude(v => v.Prescriptions)
                        .ThenInclude(p => p.PrescriptionItems)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                     a.PatientId == patient.PatientId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            return View(appointment);
        }

        public async Task<IActionResult> CancelAppointment(int id)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                     a.PatientId == patient.PatientId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            // You can only pull out before you've been checked in. Once it's CheckedIn/
            // InProgress/Completed there's nothing to cancel.
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
        public async Task<IActionResult> CancelAppointmentConfirmed(int id, string? reason)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                     a.PatientId == patient.PatientId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            // Check the status again here - the GET already checked it, but the appointment
            // could have moved on between loading the page and submitting, so don't trust it.
            if (appointment.AppointmentStatus.AppointmentStatus1 != "Requested" &&
                appointment.AppointmentStatus.AppointmentStatus1 != "Confirmed")
            {
                TempData["Error"] = "This appointment cannot be cancelled.";
                return RedirectToAction("MyAppointments");
            }

            var cancelledStatus = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "Cancelled");

            if (cancelledStatus == null)
            {
                // The status lookup table wasn't seeded properly - bail rather than crash.
                TempData["Error"] = "System error: Cancelled status not configured.";
                return RedirectToAction("MyAppointments");
            }

            appointment.AppointmentStatusId = cancelledStatus.AppointmentStatusId;
            appointment.UpdatedAt = DateTime.Now;

            // Log who changed it and why - this row is what the audit trail / history shows.
            _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentStatusId = cancelledStatus.AppointmentStatusId,
                ChangedAt = DateTime.Now,
                Notes = string.IsNullOrWhiteSpace(reason)
                    ? "Cancelled by patient"
                    : $"Cancelled by patient: {reason}",
                ChangedByAspNetUserId = patient.AspNetUserId
            });

            await _context.SaveChangesAsync();

            // Give the doctor a heads-up so the freed slot can be reused.
            await _notificationService.SendAsync(
                aspNetUserId: appointment.Doctor.AspNetUserId,
                notificationTypeName: "AppointmentCancelled",
                title: "Appointment Cancelled",
                message: $"Patient cancelled the appointment on " +
                         $"{appointment.ScheduledDate:dd MMM yyyy} at " +
                         $"{appointment.SlotStartTime:hh\\:mm}.",
                appointmentId: appointment.AppointmentId);

            TempData["Success"] = "Appointment cancelled successfully.";
            return RedirectToAction("MyAppointments");
        }

        public async Task<IActionResult> MedicalHistory()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // Their whole visit history across every doctor, newest first.
            var visitRecords = await _context.VisitRecords
                .Include(v => v.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(v => v.Appointment)
                    .ThenInclude(a => a.Specialization)
                .Where(v => v.Appointment.PatientId == patient.PatientId)
                .OrderByDescending(v => v.VisitDate)
                .ToListAsync();

            return View(visitRecords);
        }

        public async Task<IActionResult> VisitRecordDetails(int id)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var visitRecord = await _context.VisitRecords
                .Include(v => v.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(v => v.Appointment)
                    .ThenInclude(a => a.Specialization)
                .Include(v => v.Prescriptions)
                    .ThenInclude(p => p.PrescriptionItems)
                .FirstOrDefaultAsync(v => v.VisitRecordId == id &&
                                     v.Appointment.PatientId == patient.PatientId);

            if (visitRecord == null)
            {
                TempData["Error"] = "Record not found.";
                return RedirectToAction("MedicalHistory");
            }

            return View(visitRecord);
        }

        public async Task<IActionResult> MyPrescriptions()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // Prescriptions hang off the visit record, which hangs off the appointment,
            // so I have to walk back through both to filter by the patient.
            var prescriptions = await _context.Prescriptions
                .Include(p => p.VisitRecord)
                    .ThenInclude(v => v.Doctor)
                        .ThenInclude(d => d.AspNetUser)
                .Include(p => p.VisitRecord)
                    .ThenInclude(v => v.Appointment)
                .Include(p => p.PrescriptionItems)
                .Include(p => p.PrescriptionStatus)
                .Where(p => p.VisitRecord.Appointment.PatientId == patient.PatientId)
                .OrderByDescending(p => p.IssuedAt)
                .ToListAsync();

            return View(prescriptions);
        }

        public async Task<IActionResult> PrescriptionDetails(int id)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var prescription = await _context.Prescriptions
                .Include(p => p.VisitRecord)
                    .ThenInclude(v => v.Doctor)
                        .ThenInclude(d => d.AspNetUser)
                .Include(p => p.VisitRecord)
                    .ThenInclude(v => v.Appointment)
                .Include(p => p.PrescriptionItems)
                .Include(p => p.PrescriptionStatus)
                .FirstOrDefaultAsync(p => p.PrescriptionId == id &&
                                     p.VisitRecord.Appointment.PatientId == patient.PatientId);

            if (prescription == null)
            {
                TempData["Error"] = "Prescription not found.";
                return RedirectToAction("MyPrescriptions");
            }

            return View(prescription);
        }

        public async Task<IActionResult> Notifications()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var notifications = await _context.Notifications
                .Include(n => n.NotificationType)
                .Where(n => n.AspNetUserId == patient.AspNetUserId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // Opening the page counts as reading them, so clear the unread flags here.
            // Only save if there was actually something unread.
            var unread = notifications.Where(n => !n.IsRead).ToList();
            if (unread.Any())
            {
                foreach (var n in unread)
                    n.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(notifications);
        }

        public async Task<IActionResult> BookAppointment()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // Only show specializations that actually have an active doctor behind them -
            // no point offering one nobody can be booked for.
            var specializations = await _context.Specializations
                .Where(s => _context.DoctorSpecializations
                    .Any(ds => ds.SpecializationId == s.SpecializationId &&
                               ds.Doctor.IsActive))
                .ToListAsync();

            ViewBag.Specializations = specializations;
            return View();
        }

        // Called by the booking page over AJAX once a specialization is picked.
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
                        ? ds.Doctor.AspNetUser.FirstName + " " + ds.Doctor.AspNetUser.LastName
                        : "Unknown"
                })
                .ToListAsync();

            return Json(doctors);
        }

        // AJAX call that builds the list of free time slots for a doctor on a given day.
        public async Task<IActionResult> GetAvailableSlots(int doctorId, DateOnly date)
        {
            // DayOfWeek is stored as an int on the schedule, so convert the date the same way.
            int dayOfWeek = (int)date.DayOfWeek;
            var schedule = await _context.DoctorSchedules
                .FirstOrDefaultAsync(s => s.DoctorId == doctorId &&
                                     s.DayOfWeek == dayOfWeek &&
                                     s.IsActive);

            // No working hours set for that weekday => nothing to offer.
            if (schedule == null)
                return Json(new List<object>());

            var approvedLeaveStatusId = await _context.LeaveStatuses
                .Where(ls => ls.LeaveStatus1 == "Approved")
                .Select(ls => ls.LeaveStatusId)
                .FirstOrDefaultAsync();

            // If the doctor has approved leave covering this date, the day is closed.
            var isOnLeave = await _context.DoctorLeaves
                .AnyAsync(l => l.DoctorId == doctorId &&
                          l.StartDate <= date &&
                          l.EndDate >= date &&
                          l.LeaveStatusId == approvedLeaveStatusId);

            if (isOnLeave)
                return Json(new List<object>());

            // Grab every taken slot for the day in one go, then test against the set in
            // memory - cheaper than hitting the DB once per generated slot.
            var bookedSlots = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .Where(a => a.DoctorId == doctorId &&
                            a.ScheduledDate == date &&
                            a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                            a.AppointmentStatus.AppointmentStatus1 != "Missed")
                .Select(a => a.SlotStartTime)
                .ToHashSetAsync();

            // Walk the working day in SlotDurationMinutes steps and keep the open ones.
            var slots = new List<object>();
            var current = schedule.StartTime;

            while (current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)) <= schedule.EndTime)
            {
                var slotEnd = current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));

                if (!bookedSlots.Contains(current))
                {
                    slots.Add(new
                    {
                        startTime = current.ToString(),
                        endTime = slotEnd.ToString(),
                        display = current.ToString("HH\\:mm") + " - " + slotEnd.ToString("HH\\:mm")
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
            TimeOnly slotStartTime, TimeOnly slotEndTime,
            string? complaintReason)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null)
            {
                TempData["Error"] = "Patient profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // Last-second double-booking guard: someone else may have grabbed this slot
            // between loading the page and hitting submit.
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

            // Patient-booked appointments start as Requested - the doctor confirms them later.
            var appointment = new Appointment
            {
                PatientId = patient.PatientId,
                DoctorId = doctorId,
                SpecializationId = specializationId,
                ScheduledDate = scheduledDate,
                SlotStartTime = slotStartTime,
                SlotEndTime = slotEndTime,
                AppointmentStatusId = requestedStatus.AppointmentStatusId,
                ComplaintReason = complaintReason,
                CreatedByAspNetUserId = patient.AspNetUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Appointments.Add(appointment);

            // First entry in the status history. Using the navigation property here so EF
            // wires up the new AppointmentId for me on save.
            _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                Appointment = appointment,
                AppointmentStatusId = requestedStatus.AppointmentStatusId,
                ChangedAt = DateTime.Now,
                Notes = "Booked by patient",
                ChangedByAspNetUserId = patient.AspNetUserId
            });

            await _context.SaveChangesAsync();

            // Tell the doctor a new request came in so they can confirm it.
            var doctorProfile = await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctorProfile != null)
            {
                await _notificationService.SendAsync(
                    aspNetUserId: doctorProfile.AspNetUserId,
                    notificationTypeName: "AppointmentRequested",
                    title: "New Appointment Request",
                    message: $"You have a new appointment request on " +
                             $"{scheduledDate:dd MMM yyyy} at {slotStartTime:hh\\:mm}.",
                    appointmentId: appointment.AppointmentId);
            }

            TempData["Success"] = "Appointment booked successfully!";
            return RedirectToAction("MyAppointments");
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }
    }
}