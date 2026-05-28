using ClinicAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    // ClinicManager is allowed here so senior staff can perform reception tasks.
    [Authorize(Roles = "Receptionist,ClinicManager")]
    public class ReceptionistController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;

        public ReceptionistController(
            ClinicDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
        }

        //  DASHBOARD 
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

        // ALL APPOINTMENTS (with filters)
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

            // Date filter
            query = filter switch
            {
                "today" => query.Where(a => a.ScheduledDate == today),
                "upcoming" => query.Where(a => a.ScheduledDate > today),
                "past" => query.Where(a => a.ScheduledDate < today),
                "all" => query,
                _ => query.Where(a => a.ScheduledDate == today)
            };

            // Status filter
            if (!string.IsNullOrEmpty(status) && status != "all")
                query = query.Where(a => a.AppointmentStatus.AppointmentStatus1 == status);

            // Patient name / CPR search
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

        //APPOINTMENT DETAILS 
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

        // QUICK STATUS UPDATE (POST)
        // Receptionists can Confirm, CheckIn, Cancel, and MarkMissed
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

            // Include Patient so the status-change notification can read AspNetUserId.
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

            // Notify the patient about the status change (Confirmed/CheckedIn/Cancelled/Missed).
            await SendStatusChangeNotificationToPatientAsync(appointment, newStatus);

            await NotifyWaitingRoomAsync();

            TempData["Success"] = $"Appointment status updated to '{newStatus}'.";
            return RedirectToAction("AppointmentDetails", new { id });
        }

        // Receptionists can confirm, check in, cancel, or mark missed
        // They cannot start or complete the visit (that's the doctor's job)
        private bool IsValidReceptionistTransition(string from, string to)
        {
            if (to == "Cancelled" || to == "Missed") return true;

            return (from, to) switch
            {
                ("Requested", "Confirmed") => true,
                ("Confirmed", "CheckedIn") => true,
                _ => false
            };
        }

        // SEARCH PATIENT 
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

        // PATIENT PROFILE (read-only for Receptionist)
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

        //BOOK APPOINTMENT FOR PATIENT (GET)
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

        //GET DOCTORS BY SPECIALIZATION 
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

        // AJAX - GET AVAILABLE SLOTS
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
                        display = current.ToString("HH\\:mm") + " - " + slotEnd.ToString("HH\\:mm")
                    });
                }

                current = slotEnd;
            }

            return Json(slots);
        }

        //BOOK APPOINTMENT FOR PATIENT (POST)
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

            // Defence in depth: re-check slot availability on POST, not just on the GET/AJAX path.
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

            // Audit trail: reception booking lands directly in Confirmed.
            _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                Appointment = appointment,
                AppointmentStatusId = confirmedStatus.AppointmentStatusId,
                ChangedAt = DateTime.Now,
                Notes = "Booked by reception",
                ChangedByAspNetUserId = user.Id
            });

            await _context.SaveChangesAsync();

            // Notify the patient that an appointment was booked & confirmed on their behalf.
            await SendNotificationAsync(
                aspNetUserId: patient.AspNetUserId,
                notificationTypeName: "AppointmentConfirmed",
                title: "Appointment Confirmed",
                message: $"An appointment was booked for you on " +
                         $"{scheduledDate:dd MMM yyyy} at {slotStartTime:HH\\:mm} and confirmed.",
                appointmentId: appointment.AppointmentId);

            // Notify the doctor that reception booked a confirmed appointment with them.
            var doctorProfile = await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctorProfile != null)
            {
                await SendNotificationAsync(
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

        // WAITING ROOM BOARD - public-style live display
        // Marked [AllowAnonymous] so it can run on a public waiting-area screen without login.
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

            // Group appointments by status column for the board
            ViewBag.Confirmed = todayAppointments
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "Confirmed")
                .ToList();

            ViewBag.CheckedIn = todayAppointments
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "CheckedIn")
                .ToList();

            ViewBag.InProgress = todayAppointments
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "InProgress")
                .ToList();

            ViewBag.Completed = todayAppointments
                .Where(a => a.AppointmentStatus.AppointmentStatus1 == "Completed")
                .ToList();

            ViewBag.LastUpdated = DateTime.Now.ToString("HH:mm:ss");

            return View();
        }

        // NOTIFICATIONS 
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

        // Maps a new appointment status to the right patient notification.
        // appointment.Patient must be loaded (Include) before calling.
        private async Task SendStatusChangeNotificationToPatientAsync(
            Appointment appointment, string newStatus)
        {
            var patientAspNetUserId = appointment.Patient?.AspNetUserId;
            if (string.IsNullOrEmpty(patientAspNetUserId)) return;

            (string typeName, string title, string message)? payload = newStatus switch
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

                _ => null  // InProgress/Completed are silent (patient is physically present)
            };

            if (payload == null) return;

            await SendNotificationAsync(
                aspNetUserId: patientAspNetUserId,
                notificationTypeName: payload.Value.typeName,
                title: payload.Value.title,
                message: payload.Value.message,
                appointmentId: appointment.AppointmentId);
        }

        // Creates an in-system notification, creating the NotificationType on the fly if missing.
        private async Task SendNotificationAsync(
            string? aspNetUserId,
            string notificationTypeName,
            string title,
            string message,
            int? appointmentId = null)
        {
            if (string.IsNullOrEmpty(aspNetUserId)) return;

            var type = await _context.NotificationTypes
                .FirstOrDefaultAsync(t => t.Type == notificationTypeName);

            if (type == null)
            {
                type = new NotificationType { Type = notificationTypeName };
                _context.NotificationTypes.Add(type);
                await _context.SaveChangesAsync();
            }

            _context.Notifications.Add(new Notification
            {
                AspNetUserId = aspNetUserId,
                NotificationTypeId = type.NotificationTypeId,
                AppointmentId = appointmentId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        // Broadcasts a Waiting Room refresh via the SignalR hub hosted in ClinicAPI (port 7221).
        private async Task NotifyWaitingRoomAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://localhost:7221/");
                await client.PostAsync("api/waitingroom/notify-update", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR broadcast failed: {ex.Message}");
            }
        }
    }
}