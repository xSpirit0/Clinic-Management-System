using ClinicAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    // TODO: Uncomment when Auth teammate finishes Login/Register
    // [Authorize(Roles = "Receptionist")]
    public class ReceptionistController : Controller
    {
        private readonly ClinicDbContext _context;

        // TODO: Inject UserManager and SignalR hub context when ready
        // private readonly UserManager<ApplicationUser> _userManager;
        // private readonly IHubContext<WaitingRoomHub> _hubContext;

        public ReceptionistController(ClinicDbContext context)
        {
            _context = context;
        }

        // ==================== DASHBOARD ====================
        public async Task<IActionResult> Dashboard()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Today's overall numbers
            var todayAppointments = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate == today)
                .ToListAsync();

            var totalToday = todayAppointments.Count;
            var checkedIn = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "CheckedIn");
            var inProgress = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "InProgress");
            var awaiting = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "Confirmed");
            var completed = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "Completed");

            // Pending check-ins (Confirmed appointments arriving today)
            var pendingCheckIns = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate == today &&
                            a.AppointmentStatus.AppointmentStatus1 == "Confirmed")
                .OrderBy(a => a.SlotStartTime)
                .Take(10)
                .ToListAsync();

            ViewBag.TotalToday = totalToday;
            ViewBag.CheckedIn = checkedIn;
            ViewBag.InProgress = inProgress;
            ViewBag.Awaiting = awaiting;
            ViewBag.Completed = completed;
            ViewBag.PendingCheckIns = pendingCheckIns;

            return View();
        }

        // ==================== TODAY'S APPOINTMENTS ====================
        public async Task<IActionResult> TodayAppointments(string? status = null, int? doctorId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate == today);

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(a => a.AppointmentStatus.AppointmentStatus1 == status);
            }

            if (doctorId.HasValue && doctorId.Value > 0)
            {
                query = query.Where(a => a.DoctorId == doctorId.Value);
            }

            var appointments = await query
                .OrderBy(a => a.SlotStartTime)
                .ToListAsync();

            ViewBag.CurrentStatus = status ?? "all";
            ViewBag.CurrentDoctorId = doctorId ?? 0;

            ViewBag.AllStatuses = await _context.AppointmentStatuses
                .Select(s => s.AppointmentStatus1).ToListAsync();
            ViewBag.AllDoctors = await _context.DoctorProfiles
                .Include(d => d.AspNetUser)
                .Where(d => d.IsActive)
                .Select(d => new { d.DoctorId, Name = d.AspNetUser!.FirstName + " " + d.AspNetUser!.LastName })
                .ToListAsync();

            return View(appointments);
        }

        // ==================== SEARCH PATIENT ====================
        public async Task<IActionResult> SearchPatient(string? query)
        {
            List<PatientProfile> results = new();

            if (!string.IsNullOrWhiteSpace(query))
            {
                results = await _context.PatientProfiles
                    .Include(p => p.AspNetUser)
                    .Where(p =>
                        p.Cprnumber.Contains(query) ||
                        p.PatientReferenceNumber.Contains(query) ||
                        ((p.AspNetUser!.FirstName != null && p.AspNetUser.FirstName.Contains(query)) ||
                         (p.AspNetUser.LastName != null && p.AspNetUser.LastName.Contains(query))))
                    .Take(20)
                    .ToListAsync();
            }

            ViewBag.Query = query;
            return View(results);
        }

        // ==================== BOOK FOR PATIENT (GET) ====================
        // Receptionist books an appointment on behalf of a patient
        public async Task<IActionResult> BookForPatient(int? patientId)
        {
            if (!patientId.HasValue)
            {
                TempData["Error"] = "Please search and select a patient first.";
                return RedirectToAction("SearchPatient");
            }

            var patient = await _context.PatientProfiles
                .Include(p => p.AspNetUser)
                .FirstOrDefaultAsync(p => p.PatientId == patientId.Value);

            if (patient == null)
            {
                TempData["Error"] = "Patient not found.";
                return RedirectToAction("SearchPatient");
            }

            var specializations = await _context.Specializations
                .Where(s => _context.DoctorSpecializations
                    .Any(ds => ds.SpecializationId == s.SpecializationId &&
                               ds.Doctor.IsActive))
                .ToListAsync();

            ViewBag.Patient = patient;
            ViewBag.Specializations = specializations;

            return View();
        }

        // ==================== AJAX: Get doctors by specialization ====================
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

        // ==================== AJAX: Get available slots ====================
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
                .Select(ls => ls.LeaveStatusId).FirstOrDefaultAsync();

            var isOnLeave = await _context.DoctorLeaves
                .AnyAsync(l => l.DoctorId == doctorId &&
                          l.StartDate <= date && l.EndDate >= date &&
                          l.LeaveStatusId == approvedLeaveStatusId);
            if (isOnLeave)
                return Json(new List<object>());

            var slots = new List<object>();
            var current = schedule.StartTime;

            while (current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)) <= schedule.EndTime)
            {
                var slotEnd = current.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));

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
                        display = current.ToString("hh\\:mm") + " - " + slotEnd.ToString("hh\\:mm")
                    });
                }
                current = slotEnd;
            }

            return Json(slots);
        }

        // ==================== BOOK FOR PATIENT (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookForPatient(
            int patientId, int specializationId, int doctorId,
            DateOnly scheduledDate, TimeOnly slotStartTime, TimeOnly slotEndTime,
            string? complaintReason)
        {
            // Receptionist books on behalf — confirm immediately (skip "Requested")
            var confirmedStatus = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "Confirmed");

            if (confirmedStatus == null)
            {
                TempData["Error"] = "System error: Confirmed status not configured.";
                return RedirectToAction("BookForPatient", new { patientId });
            }

            var isBooked = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .AnyAsync(a => a.DoctorId == doctorId &&
                          a.ScheduledDate == scheduledDate &&
                          a.SlotStartTime == slotStartTime &&
                          a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                          a.AppointmentStatus.AppointmentStatus1 != "Missed");

            if (isBooked)
            {
                TempData["Error"] = "This slot was just booked by someone else. Please pick another.";
                return RedirectToAction("BookForPatient", new { patientId });
            }

            // TODO: When auth ready, set CreatedByAspNetUserId from logged-in receptionist
            var appointment = new Appointment
            {
                PatientId = patientId,
                DoctorId = doctorId,
                SpecializationId = specializationId,
                ScheduledDate = scheduledDate,
                SlotStartTime = slotStartTime,
                SlotEndTime = slotEndTime,
                AppointmentStatusId = confirmedStatus.AppointmentStatusId,
                ComplaintReason = complaintReason,
                CreatedByAspNetUserId = null,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // TODO: Notification to patient + doctor
            // TODO: SignalR broadcast for new appointment (Chunk 2)

            TempData["Success"] = "Appointment booked and confirmed for the patient.";
            return RedirectToAction("TodayAppointments");
        }

        // ==================== CHECK IN PATIENT ====================
        // Will be expanded in Chunk 3 with a confirmation view
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("TodayAppointments");
            }

            if (appointment.AppointmentStatus.AppointmentStatus1 != "Confirmed")
            {
                TempData["Error"] = $"Cannot check in an appointment that is '{appointment.AppointmentStatus.AppointmentStatus1}'.";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            var checkedInStatus = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "CheckedIn");
            if (checkedInStatus == null)
            {
                TempData["Error"] = "System error: CheckedIn status not configured.";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            appointment.AppointmentStatusId = checkedInStatus.AppointmentStatusId;
            appointment.UpdatedAt = DateTime.Now;

            _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentStatusId = checkedInStatus.AppointmentStatusId,
                ChangedAt = DateTime.Now,
                Notes = "Checked in by receptionist.",
                ChangedByAspNetUserId = null
            });

            await _context.SaveChangesAsync();

            // TODO: SignalR broadcast — waiting room board updates (Chunk 2)

            TempData["Success"] = "Patient checked in.";
            return RedirectToAction("TodayAppointments");
        }

        // ==================== CANCEL APPOINTMENT ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int id, string? reason)
        {
            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("TodayAppointments");
            }

            var current = appointment.AppointmentStatus.AppointmentStatus1;
            if (current == "Completed" || current == "Cancelled" || current == "Missed")
            {
                TempData["Error"] = $"Cannot cancel an appointment that is '{current}'.";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            var cancelledStatus = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "Cancelled");
            if (cancelledStatus == null)
            {
                TempData["Error"] = "System error: Cancelled status not configured.";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            appointment.AppointmentStatusId = cancelledStatus.AppointmentStatusId;
            appointment.UpdatedAt = DateTime.Now;

            _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentStatusId = cancelledStatus.AppointmentStatusId,
                ChangedAt = DateTime.Now,
                Notes = reason ?? "Cancelled by receptionist.",
                ChangedByAspNetUserId = null
            });

            await _context.SaveChangesAsync();

            // TODO: Notify patient + doctor; SignalR broadcast

            TempData["Success"] = "Appointment cancelled.";
            return RedirectToAction("TodayAppointments");
        }

        // ==================== APPOINTMENT DETAILS ====================
        // Filled in Chunk 3
        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                .Include(a => a.AppointmentStatusHistories.OrderByDescending(h => h.ChangedAt))
                    .ThenInclude(h => h.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("TodayAppointments");
            }

            return View(appointment);
        }

        // ==================== WAITING ROOM BOARD ====================
        // The big SignalR view — built in Chunk 3
        public async Task<IActionResult> WaitingRoomBoard()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var todayAppointments = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate == today &&
                            (a.AppointmentStatus.AppointmentStatus1 == "Confirmed" ||
                             a.AppointmentStatus.AppointmentStatus1 == "CheckedIn" ||
                             a.AppointmentStatus.AppointmentStatus1 == "InProgress"))
                .OrderBy(a => a.SlotStartTime)
                .ToListAsync();

            return View(todayAppointments);
        }

        // ==================== NOTIFICATIONS ====================
        public async Task<IActionResult> Notifications()
        {
            // TODO: When auth ready
            var notifications = new List<Notification>();
            return View(notifications);
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }
    }
}
