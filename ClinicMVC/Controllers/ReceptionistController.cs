using ClinicAPI.Models;
using ClinicMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    [Authorize(Roles = "Receptionist,ClinicManager")]
    public class ReceptionistController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly INotificationService _notificationService;

        public ReceptionistController(
            ClinicDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpClientFactory httpClientFactory,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["Error"] = "User profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            var todayAppointments = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate == today)
                .OrderBy(a => a.SlotStartTime)
                .ToListAsync();

            var totalToday = todayAppointments.Count;
            var pendingToday = todayAppointments.Count(a =>
                a.AppointmentStatus.AppointmentStatus1 == "Requested" ||
                a.AppointmentStatus.AppointmentStatus1 == "Confirmed");
            var checkedIn = todayAppointments.Count(a =>
                a.AppointmentStatus.AppointmentStatus1 == "CheckedIn" ||
                a.AppointmentStatus.AppointmentStatus1 == "InProgress");
            var completedToday = todayAppointments.Count(a =>
                a.AppointmentStatus.AppointmentStatus1 == "Completed");

            ViewBag.TodayAppointments = todayAppointments;
            ViewBag.TotalToday = totalToday;
            ViewBag.PendingToday = pendingToday;
            ViewBag.CheckedIn = checkedIn;
            ViewBag.CompletedToday = completedToday;

            ViewBag.UnreadNotifications = await _context.Notifications
                .CountAsync(n => n.AspNetUserId == user.Id && !n.IsRead);

            return View();
        }

        public async Task<IActionResult> AllAppointments(
            string filter = "today",
            string? status = null,
            string? search = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .AsQueryable();

            query = filter switch
            {
                "today" => query.Where(a => a.ScheduledDate == today),
                "upcoming" => query.Where(a => a.ScheduledDate > today),
                "past" => query.Where(a => a.ScheduledDate < today),
                "all" => query,
                _ => query.Where(a => a.ScheduledDate == today)
            };

            if (!string.IsNullOrEmpty(status) && status != "all")
                query = query.Where(a => a.AppointmentStatus.AppointmentStatus1 == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(a =>
                    (a.Patient.AspNetUser != null &&
                     (a.Patient.AspNetUser.FirstName.Contains(search) ||
                      a.Patient.AspNetUser.LastName.Contains(search))) ||
                    a.Patient.Cprnumber.Contains(search));
            }

            var appointments = await query
                .OrderBy(a => a.ScheduledDate)
                .ThenBy(a => a.SlotStartTime)
                .ToListAsync();

            ViewBag.CurrentFilter = filter;
            ViewBag.CurrentStatus = status ?? "all";
            ViewBag.Search = search ?? "";
            ViewBag.AllStatuses = await _context.AppointmentStatuses
                .Select(s => s.AppointmentStatus1)
                .ToListAsync();

            return View(appointments);
        }

        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.AppointmentStatusHistories
                    .OrderByDescending(h => h.ChangedAt))
                    .ThenInclude(h => h.AppointmentStatus)
                .Include(a => a.VisitRecord)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("AllAppointments");
            }

            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus, string? notes)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["Error"] = "User profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("AllAppointments");
            }

            var currentStatus = appointment.AppointmentStatus.AppointmentStatus1;
            var allowed = IsValidReceptionistTransition(currentStatus, newStatus);
            if (!allowed)
            {
                TempData["Error"] = $"Cannot transition from '{currentStatus}' to '{newStatus}'.";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            var newStatusEntity = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == newStatus);

            if (newStatusEntity == null)
            {
                TempData["Error"] = $"Status '{newStatus}' not found.";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            appointment.AppointmentStatusId = newStatusEntity.AppointmentStatusId;
            appointment.UpdatedAt = DateTime.Now;

            _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentStatusId = newStatusEntity.AppointmentStatusId,
                ChangedAt = DateTime.Now,
                Notes = notes,
                ChangedByAspNetUserId = user.Id
            });

            await _context.SaveChangesAsync();
            await SendStatusChangeNotificationsAsync(appointment, newStatus);
            await NotifyWaitingRoomAsync();

            TempData["Success"] = $"Appointment status updated to '{newStatus}'.";
            return RedirectToAction("AppointmentDetails", new { id });
        }

        private bool IsValidReceptionistTransition(string from, string to)
        {
            return (from, to) switch
            {
                ("Requested", "Confirmed") => true,
                ("Confirmed", "CheckedIn") => true,
                ("Requested", "Cancelled") => true,
                ("Confirmed", "Cancelled") => true,
                ("Confirmed", "Missed") => true,
                ("CheckedIn", "Missed") => true,
                _ => false
            };
        }

        public async Task<IActionResult> SearchPatient(string? query)
        {
            var patients = new List<PatientProfile>();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.Trim();
                patients = await _context.PatientProfiles
                    .Include(p => p.AspNetUser)
                    .Where(p =>
                        (p.AspNetUser != null &&
                         (p.AspNetUser.FirstName.Contains(query) ||
                          p.AspNetUser.LastName.Contains(query))) ||
                        p.Cprnumber.Contains(query) ||
                        p.PatientReferenceNumber.Contains(query))
                    .OrderBy(p => p.AspNetUser!.FirstName)
                    .ToListAsync();
            }

            ViewBag.Query = query ?? "";
            return View(patients);
        }

        public async Task<IActionResult> PatientProfile(int id)
        {
            var patient = await _context.PatientProfiles
                .Include(p => p.AspNetUser)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null)
            {
                TempData["Error"] = "Patient not found.";
                return RedirectToAction("SearchPatient");
            }

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.PatientId == id)
                .OrderByDescending(a => a.ScheduledDate)
                .Take(10)
                .ToListAsync();

            ViewBag.Appointments = appointments;
            return View(patient);
        }

        public async Task<IActionResult> BookAppointment(int? patientId)
        {
            PatientProfile? patient = null;
            if (patientId.HasValue)
            {
                patient = await _context.PatientProfiles
                    .Include(p => p.AspNetUser)
                    .FirstOrDefaultAsync(p => p.PatientId == patientId.Value);
            }

            var specializations = await _context.Specializations
                .Where(s => _context.DoctorSpecializations
                    .Any(ds => ds.SpecializationId == s.SpecializationId &&
                               ds.Doctor.IsActive))
                .ToListAsync();

            ViewBag.Specializations = specializations;
            ViewBag.SelectedPatient = patient;

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
                        ? ds.Doctor.AspNetUser.FirstName + " " + ds.Doctor.AspNetUser.LastName
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

            var bookedSlots = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .Where(a => a.DoctorId == doctorId &&
                            a.ScheduledDate == date &&
                            a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                            a.AppointmentStatus.AppointmentStatus1 != "Missed")
                .Select(a => a.SlotStartTime)
                .ToHashSetAsync();

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
            int patientId,
            int specializationId,
            int doctorId,
            DateOnly scheduledDate,
            TimeOnly slotStartTime,
            TimeOnly slotEndTime,
            string? complaintReason)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["Error"] = "User profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var patient = await _context.PatientProfiles
                .Include(p => p.AspNetUser)
                .FirstOrDefaultAsync(p => p.PatientId == patientId);

            if (patient == null)
            {
                TempData["Error"] = "Patient not found.";
                return RedirectToAction("BookAppointment");
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
                TempData["Error"] = "This slot is no longer available. Please choose another.";
                return RedirectToAction("BookAppointment", new { patientId });
            }

            var confirmedStatus = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "Confirmed");

            if (confirmedStatus == null)
            {
                TempData["Error"] = "System error: Confirmed status not configured.";
                return RedirectToAction("BookAppointment", new { patientId });
            }

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
                CreatedByAspNetUserId = user.Id,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Appointments.Add(appointment);

            _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                Appointment = appointment,
                AppointmentStatusId = confirmedStatus.AppointmentStatusId,
                ChangedAt = DateTime.Now,
                Notes = "Booked by reception",
                ChangedByAspNetUserId = user.Id
            });

            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                aspNetUserId: patient.AspNetUserId,
                notificationTypeName: "AppointmentConfirmed",
                title: "Appointment Confirmed",
                message: $"An appointment was booked for you on " +
                         $"{scheduledDate:dd MMM yyyy} at {slotStartTime:HH\\:mm} and confirmed.",
                appointmentId: appointment.AppointmentId);

            var doctorProfile = await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctorProfile != null)
            {
                await _notificationService.SendAsync(
                    aspNetUserId: doctorProfile.AspNetUserId,
                    notificationTypeName: "AppointmentConfirmed",
                    title: "New Confirmed Appointment",
                    message: $"Reception booked a confirmed appointment on " +
                             $"{scheduledDate:dd MMM yyyy} at {slotStartTime:HH\\:mm}.",
                    appointmentId: appointment.AppointmentId);
            }

            await NotifyWaitingRoomAsync();

            TempData["Success"] =
                $"Appointment booked successfully for {scheduledDate:dd MMM yyyy} at {slotStartTime:HH\\:mm}.";

            return RedirectToAction("PatientProfile", new { id = patientId });
        }

        [AllowAnonymous]
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
                            a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                            a.AppointmentStatus.AppointmentStatus1 != "Missed")
                .OrderBy(a => a.SlotStartTime)
                .ToListAsync();

            ViewBag.Confirmed = todayAppointments
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "Confirmed").ToList();
            ViewBag.CheckedIn = todayAppointments
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "CheckedIn").ToList();
            ViewBag.InProgress = todayAppointments
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "InProgress").ToList();
            ViewBag.Completed = todayAppointments
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "Completed").ToList();

            ViewBag.LastUpdated = DateTime.Now.ToString("HH:mm:ss");

            return View();
        }

        public async Task<IActionResult> Notifications()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["Error"] = "User profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var notifications = await _context.Notifications
                .Include(n => n.NotificationType)
                .Where(n => n.AspNetUserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var unread = notifications.Where(n => !n.IsRead).ToList();
            if (unread.Any())
            {
                foreach (var n in unread) n.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(notifications);
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        // ==================== HELPERS ====================

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task SendStatusChangeNotificationsAsync(
            Appointment appointment, string newStatus)
        {
            // --- Patient notification ---
            var patientAspNetUserId = appointment.Patient?.AspNetUserId;
            if (!string.IsNullOrEmpty(patientAspNetUserId))
            {
                (string typeName, string title, string message)? patientPayload = newStatus switch
                {
                    "Confirmed" => (
                        "AppointmentConfirmed",
                        "Appointment Confirmed",
                        $"Your appointment on {appointment.ScheduledDate:dd MMM yyyy} at " +
                            $"{appointment.SlotStartTime:HH\\:mm} has been confirmed."),

                    "CheckedIn" => (
                        "PatientCheckedIn",
                        "You've Been Checked In",
                        $"You've been checked in for your appointment at " +
                            $"{appointment.SlotStartTime:HH\\:mm}. Please take a seat."),

                    "Cancelled" => (
                        "AppointmentCancelled",
                        "Appointment Cancelled",
                        $"Your appointment on {appointment.ScheduledDate:dd MMM yyyy} at " +
                            $"{appointment.SlotStartTime:HH\\:mm} was cancelled by the clinic."),

                    "Missed" => (
                        "AppointmentMissed",
                        "Appointment Marked Missed",
                        $"Your appointment on {appointment.ScheduledDate:dd MMM yyyy} at " +
                            $"{appointment.SlotStartTime:HH\\:mm} was marked as missed."),

                    _ => null
                };

                if (patientPayload != null)
                    await _notificationService.SendAsync(
                        aspNetUserId: patientAspNetUserId,
                        notificationTypeName: patientPayload.Value.typeName,
                        title: patientPayload.Value.title,
                        message: patientPayload.Value.message,
                        appointmentId: appointment.AppointmentId);
            }

            // --- Doctor notification ---
            var doctorProfile = await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.DoctorId == appointment.DoctorId);

            if (doctorProfile != null)
            {
                (string typeName, string title, string message)? doctorPayload = newStatus switch
                {
                    "Cancelled" => (
                        "AppointmentCancelled",
                        "Appointment Cancelled",
                        $"An appointment on {appointment.ScheduledDate:dd MMM yyyy} at " +
                            $"{appointment.SlotStartTime:HH\\:mm} has been cancelled."),

                    "Missed" => (
                        "AppointmentMissed",
                        "Appointment Marked Missed",
                        $"An appointment on {appointment.ScheduledDate:dd MMM yyyy} at " +
                            $"{appointment.SlotStartTime:HH\\:mm} was marked as missed."),

                    _ => null
                };

                if (doctorPayload != null)
                    await _notificationService.SendAsync(
                        aspNetUserId: doctorProfile.AspNetUserId,
                        notificationTypeName: doctorPayload.Value.typeName,
                        title: doctorPayload.Value.title,
                        message: doctorPayload.Value.message,
                        appointmentId: appointment.AppointmentId);
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> WaitingRoomData()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var todayAppointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor).ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate == today &&
                            a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                            a.AppointmentStatus.AppointmentStatus1 != "Missed")
                .OrderBy(a => a.SlotStartTime)
                .Select(a => new
                {
                    status = a.AppointmentStatus.AppointmentStatus1,
                    time = a.SlotStartTime.ToString("HH:mm"),
                    patientName = (a.Patient.AspNetUser != null
                        ? a.Patient.AspNetUser.FirstName + " " + a.Patient.AspNetUser.LastName
                        : "Unknown"),
                    doctorName = (a.Doctor.AspNetUser != null
                        ? a.Doctor.AspNetUser.FirstName
                        : "—"),
                    specialization = a.Specialization != null ? a.Specialization.Name : ""
                })
                .ToListAsync();

            return Json(new
            {
                lastUpdated = DateTime.Now.ToString("HH:mm:ss"),
                confirmed = todayAppointments.Where(a => a.status == "Confirmed"),
                checkedIn = todayAppointments.Where(a => a.status == "CheckedIn"),
                inProgress = todayAppointments.Where(a => a.status == "InProgress"),
                completed = todayAppointments.Where(a => a.status == "Completed")
            });
        }
        private async Task NotifyWaitingRoomAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ClinicApi");
                await client.PostAsync("api/waitingroom/notify-update", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR broadcast failed: {ex.Message}");
            }
        }
    }
}
