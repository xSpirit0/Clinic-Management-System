using ClinicAPI.Models;
using ClinicMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    [Authorize(Roles = "Receptionist,ClinicManager")] // Only users in the Receptionist or ClinicManager roles can access this controller
    public class ReceptionistController : Controller
    {
        // Dependencies for database access, user management, HTTP requests, and notifications
        private readonly ClinicDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly INotificationService _notificationService;

        // Constructor to inject dependencies
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

        // This is the main dashboard for the receptionist, showing an overview of today's appointments, including counts of total, pending, checked-in, and completed appointments. It also shows a list of today's appointments with key details and a link to view more information. Additionally, it displays any unread notifications for the receptionist.
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

        // This page shows a list of appointments with filters for date range (today, upcoming, past, all), status, and a search box for patient name or CPR number. The receptionist can click on an appointment to view details and update the status if needed.
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

        //  This page shows detailed information about a specific appointment, including the patient's profile, the doctor's profile, the specialization, the current status, and the history of status changes. It also includes a form for updating the status if the receptionist has permission to do so.
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

        // This action handles the form submission for updating the appointment status. It checks if the transition is valid for the receptionist role, updates the status, logs the change in the history table, and sends notifications to the patient and doctor if necessary.
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

        // This method defines which status transitions are allowed for the receptionist role. For example, they can confirm a requested appointment, check in a confirmed appointment, or cancel an appointment that is still pending.
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

        // This page allows the receptionist to search for patients by name, CPR number, or patient reference number. The search results show basic profile information and a link to view the full profile and appointment history.
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

        // This page shows the patient's profile information and a list of their upcoming and recent appointments, with links to view details for each appointment.
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

        // This page shows the booking form where the receptionist can select a patient (or leave it blank to create a new one), choose a specialization, doctor, date, and time slot, and enter the reason for the visit. The form uses AJAX to dynamically load doctors based on the selected specialization and available time slots based on the selected doctor and date.
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

        // This endpoint is called by the booking form via AJAX when the receptionist selects a specialization, to populate the doctor dropdown with only those doctors who have that specialization and are currently active.
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

        // This endpoint is called by the booking form via AJAX when the receptionist selects a doctor and date, to get the available time slots for that doctor on that day. It checks the doctor's schedule, any approved leaves, and existing appointments to determine which slots are still open.
        public async Task<IActionResult> GetAvailableSlots(int doctorId, DateOnly date)
        {
            int dayOfWeek = (int)date.DayOfWeek;
            // Get the doctor's schedule for that day of the week.
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
            // Get all booked slots for that doctor and date, excluding cancelled and missed appointments.
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

        // This action handles the form submission for booking a new appointment. It performs server-side validation to ensure the slot is still available and then creates the appointment with a "Confirmed" status. It also sends notifications to the patient and doctor about the new appointment.
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

            // Double-check if the doctor is on leave for that day, in case their schedule was updated after the receptionist loaded the booking page.
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

            // Send notification to patient about the new appointment.
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

        // This page is displayed on a TV screen in the waiting room, showing the status of today's appointments.
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

        // This page shows all notifications for the logged-in user, with unread ones highlighted. When the receptionist visits this page, all their unread notifications will be marked as read.
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

        // HELPERS 
        // Get the currently logged-in user from the database, including their profile information.
        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task SendStatusChangeNotificationsAsync(
            Appointment appointment, string newStatus)
        {
            //  Patient notification 
            // Only notify for key status changes that the patient should be aware of. For example, "CheckedIn" is important for the patient to know, but "InProgress" might not be necessary as they are already in the clinic.
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

            // Doctor notification 
            // Only notify for cancellations and missed appointments, as other status changes are typically handled by the doctor themselves.
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

        // This endpoint is called by the waiting room board via AJAX every 30 seconds to get the latest data.
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

        // This method is called after any status change that could affect the waiting room display.
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
