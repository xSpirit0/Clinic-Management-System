using ClinicAPI.Models;
using ClinicMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public DoctorController(
            ClinicDbContext context,
            IHttpClientFactory httpClientFactory,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // Logged-in user -> their DoctorProfile. Everything filters on DoctorId, so I
        // resolve it once and the actions bail to login if there's no profile.
        private async Task<DoctorProfile?> GetCurrentDoctorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            return await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.AspNetUserId == user.Id);
        }

        public async Task<IActionResult> Dashboard()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            var todayAppointments = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.DoctorId == doctor.DoctorId &&
                            a.ScheduledDate == today)
                .OrderBy(a => a.SlotStartTime)
                .ToListAsync();

            // Counting in memory off the list I already loaded instead of firing more
            // queries - these all come from todayAppointments.
            var totalToday = todayAppointments.Count;
            var completedToday = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "Completed");
            var inProgressNow = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "InProgress");
            var pendingToday = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "Requested" ||
                            a.AppointmentStatus.AppointmentStatus1 == "Confirmed" ||
                            a.AppointmentStatus.AppointmentStatus1 == "CheckedIn");

            // Next 7 days, skipping the dead ones (cancelled/missed) so the panel stays useful.
            var upcomingWeek = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.DoctorId == doctor.DoctorId &&
                            a.ScheduledDate > today &&
                            a.ScheduledDate <= today.AddDays(7) &&
                            a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                            a.AppointmentStatus.AppointmentStatus1 != "Missed")
                .OrderBy(a => a.ScheduledDate)
                .ThenBy(a => a.SlotStartTime)
                .ToListAsync();

            ViewBag.UnreadNotifications = await _context.Notifications
                .CountAsync(n => n.AspNetUserId == doctor.AspNetUserId && !n.IsRead);

            ViewBag.TodayAppointments = todayAppointments;
            ViewBag.UpcomingWeek = upcomingWeek;
            ViewBag.TotalToday = totalToday;
            ViewBag.CompletedToday = completedToday;
            ViewBag.InProgressNow = inProgressNow;
            ViewBag.PendingToday = pendingToday;

            return View();
        }

        // filter = today/upcoming/past/all (date range), status = optional status filter on top.
        public async Task<IActionResult> MyAppointments(string filter = "today", string? status = null)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            // Build the query in steps and only hit the DB at the ToListAsync at the end.
            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.DoctorId == doctor.DoctorId);

            query = filter switch
            {
                "today" => query.Where(a => a.ScheduledDate == today),
                "upcoming" => query.Where(a => a.ScheduledDate > today),
                "past" => query.Where(a => a.ScheduledDate < today),
                "all" => query,
                _ => query.Where(a => a.ScheduledDate == today)
            };

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(a => a.AppointmentStatus.AppointmentStatus1 == status);
            }

            var appointments = await query
                .OrderBy(a => a.ScheduledDate)
                .ThenBy(a => a.SlotStartTime)
                .ToListAsync();

            // Pass the current selections back so the filter dropdowns stay on what was picked.
            ViewBag.CurrentFilter = filter;
            ViewBag.CurrentStatus = status ?? "all";

            ViewBag.AllStatuses = await _context.AppointmentStatuses
                .Select(s => s.AppointmentStatus1)
                .ToListAsync();

            return View(appointments);
        }

        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // DoctorId in the filter stops a doctor opening an appointment that isn't theirs.
            // Also pulling the status history (newest first) for the timeline on the page.
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                    .ThenInclude(v => v != null ? v.Prescriptions : null)
                        .ThenInclude(p => p.PrescriptionItems)
                .Include(a => a.AppointmentStatusHistories.OrderByDescending(h => h.ChangedAt))
                    .ThenInclude(h => h.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                          a.DoctorId == doctor.DoctorId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found or not assigned to you.";
                return RedirectToAction("MyAppointments");
            }

            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus, string? notes)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                          a.DoctorId == doctor.DoctorId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            var newStatusEntity = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == newStatus);

            if (newStatusEntity == null)
            {
                TempData["Error"] = $"Invalid status: {newStatus}";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            // Don't let the status jump around - only the moves in IsValidTransition are allowed.
            var currentStatus = appointment.AppointmentStatus.AppointmentStatus1;
            if (!IsValidTransition(currentStatus, newStatus))
            {
                TempData["Error"] = $"Cannot transition from '{currentStatus}' to '{newStatus}'.";
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
                ChangedByAspNetUserId = doctor.AspNetUserId
            });

            await _context.SaveChangesAsync();
            await NotifyWaitingRoomAsync();                            // refresh the live board
            await SendStatusChangeNotificationsAsync(appointment, newStatus);

            TempData["Success"] = $"Appointment status updated to '{newStatus}'.";
            return RedirectToAction("AppointmentDetails", new { id });
        }

        // The appointment workflow as a simple state machine. Each tuple is an allowed
        // (current -> next) move; everything else returns false and gets blocked above.
        private bool IsValidTransition(string from, string to)
        {
            return (from, to) switch
            {
                ("Requested", "Confirmed") => true,
                ("Confirmed", "CheckedIn") => true,
                ("CheckedIn", "InProgress") => true,
                ("InProgress", "Completed") => true,
                ("Confirmed", "Missed") => true,
                ("CheckedIn", "Missed") => true,
                ("InProgress", "Missed") => true,
                _ => false
            };
        }

        public async Task<IActionResult> WriteVisitRecord(int appointmentId)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId &&
                                          a.DoctorId == doctor.DoctorId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            // No point writing notes for a visit that hasn't started yet.
            if (appointment.AppointmentStatus.AppointmentStatus1 != "InProgress" &&
                appointment.AppointmentStatus.AppointmentStatus1 != "Completed")
            {
                TempData["Error"] = "Visit record can only be written for an in-progress or completed appointment.";
                return RedirectToAction("AppointmentDetails", new { id = appointmentId });
            }

            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WriteVisitRecord(int appointmentId,
                                                          string? doctorNotes,
                                                          string? diagnosis,
                                                          string? treatment)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId &&
                                          a.DoctorId == doctor.DoctorId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            // Remember whether it was already Completed - I use this later to avoid
            // re-notifying the patient every time the doctor edits the notes.
            bool wasAlreadyCompleted =
                appointment.AppointmentStatus.AppointmentStatus1 == "Completed";

            // One visit record per appointment: update it if it exists, otherwise create it.
            if (appointment.VisitRecord != null)
            {
                appointment.VisitRecord.DoctorNotes = doctorNotes;
                appointment.VisitRecord.Diagnosis = diagnosis;
                appointment.VisitRecord.Treatment = treatment;
            }
            else
            {
                var visit = new VisitRecord
                {
                    AppointmentId = appointmentId,
                    DoctorId = doctor.DoctorId,
                    DoctorNotes = doctorNotes,
                    Diagnosis = diagnosis,
                    Treatment = treatment,
                    VisitDate = DateTime.Now
                };
                _context.VisitRecords.Add(visit);
            }

            // Writing the record is what flips the appointment to Completed (first time only).
            if (!wasAlreadyCompleted)
            {
                var completedStatus = await _context.AppointmentStatuses
                    .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "Completed");
                if (completedStatus != null)
                {
                    appointment.AppointmentStatusId = completedStatus.AppointmentStatusId;
                    appointment.UpdatedAt = DateTime.Now;

                    _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appointment.AppointmentId,
                        AppointmentStatusId = completedStatus.AppointmentStatusId,
                        ChangedAt = DateTime.Now,
                        Notes = "Visit completed.",
                        ChangedByAspNetUserId = doctor.AspNetUserId
                    });
                }
            }

            await _context.SaveChangesAsync();
            await NotifyWaitingRoomAsync();

            // Only ping the patient on the first completion, not on later edits.
            if (!wasAlreadyCompleted)
            {
                await _notificationService.SendAsync(
                    aspNetUserId: appointment.Patient.AspNetUserId,
                    notificationTypeName: "VisitCompleted",
                    title: "Visit Completed",
                    message: $"Your visit on {appointment.ScheduledDate:dd MMM yyyy} has been completed. " +
                             "You can view the visit record and any prescriptions in your medical history.",
                    appointmentId: appointment.AppointmentId);
            }

            TempData["Success"] = "Visit record saved.";
            return RedirectToAction("AppointmentDetails", new { id = appointmentId });
        }

        public async Task<IActionResult> AddPrescription(int visitRecordId)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var visit = await _context.VisitRecords
                .Include(v => v.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.AspNetUser)
                .Include(v => v.Prescriptions)
                    .ThenInclude(p => p.PrescriptionItems)
                .FirstOrDefaultAsync(v => v.VisitRecordId == visitRecordId &&
                                          v.DoctorId == doctor.DoctorId);

            if (visit == null)
            {
                TempData["Error"] = "Visit record not found or not yours.";
                return RedirectToAction("MyAppointments");
            }

            return View(visit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescription(int visitRecordId,
                                                         List<string> medicationName,
                                                         List<string> dosage,
                                                         List<string> frequency,
                                                         List<int?> durationDays,
                                                         List<string?> instructions)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var visit = await _context.VisitRecords
                .Include(v => v.Appointment)
                    .ThenInclude(a => a.Patient)
                .FirstOrDefaultAsync(v => v.VisitRecordId == visitRecordId &&
                                          v.DoctorId == doctor.DoctorId);

            if (visit == null)
            {
                TempData["Error"] = "Visit record not found.";
                return RedirectToAction("MyAppointments");
            }

            // The form posts each column as a parallel list (row 0 across all lists is one
            // medication, etc). Keep only the rows where a name was actually typed.
            var validIndexes = new List<int>();
            for (int i = 0; i < medicationName.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(medicationName[i]))
                    validIndexes.Add(i);
            }

            if (!validIndexes.Any())
            {
                TempData["Error"] = "Please add at least one medication.";
                return RedirectToAction("AddPrescription", new { visitRecordId });
            }

            var activeStatus = await _context.PrescriptionStatuses
                .FirstOrDefaultAsync(s => s.PrescriptionStatus1 == "Active");

            if (activeStatus == null)
            {
                TempData["Error"] = "System error: Active status not configured.";
                return RedirectToAction("AddPrescription", new { visitRecordId });
            }

            // Build the prescription with its items from the kept rows in one go.
            var prescription = new Prescription
            {
                VisitRecordId = visitRecordId,
                IssuedAt = DateTime.Now,
                PrescriptionStatusId = activeStatus.PrescriptionStatusId,
                PrescriptionItems = validIndexes.Select(i => new PrescriptionItem
                {
                    MedicationName = medicationName[i],
                    Dosage = dosage[i],
                    Frequency = frequency[i],
                    DurationDays = durationDays[i],
                    Instructions = instructions[i]
                }).ToList()
            };

            _context.Prescriptions.Add(prescription);
            await _context.SaveChangesAsync();
            await NotifyWaitingRoomAsync();

            await _notificationService.SendAsync(
                aspNetUserId: visit.Appointment.Patient.AspNetUserId,
                notificationTypeName: "PrescriptionIssued",
                title: "Prescription Issued",
                message: $"A new prescription with {validIndexes.Count} medication(s) has been issued. " +
                         "You can view the details in your prescriptions list.",
                appointmentId: visit.AppointmentId);

            TempData["Success"] = $"Prescription with {validIndexes.Count} medication(s) added.";
            return RedirectToAction("AppointmentDetails", new { id = visit.AppointmentId });
        }

        public async Task<IActionResult> MyPatients()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // Distinct patients I've had at least one appointment with, sorted by name.
            var patients = await _context.Appointments
                .Where(a => a.DoctorId == doctor.DoctorId)
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Select(a => a.Patient)
                .Distinct()
                .OrderBy(p => p.AspNetUser!.FirstName)
                .ThenBy(p => p.AspNetUser!.LastName)
                .ToListAsync();

            return View(patients);
        }

        public async Task<IActionResult> PatientHistory(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            // Privacy guard: a doctor can only see the history of a patient they've actually
            // treated, so check there's a shared appointment before showing anything.
            var hasSeen = await _context.Appointments
                .AnyAsync(a => a.PatientId == id && a.DoctorId == doctor.DoctorId);

            if (!hasSeen)
            {
                TempData["Error"] = "You can only view history for your own patients.";
                return RedirectToAction("MyPatients");
            }

            var patient = await _context.PatientProfiles
                .Include(p => p.AspNetUser)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null)
            {
                TempData["Error"] = "Patient not found.";
                return RedirectToAction("MyPatients");
            }

            // Their full appointment history (across every doctor, not just me).
            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                    .ThenInclude(v => v != null ? v.Prescriptions : null)
                        .ThenInclude(p => p.PrescriptionItems)
                .Where(a => a.PatientId == id)
                .OrderByDescending(a => a.ScheduledDate)
                .ToListAsync();

            ViewBag.Patient = patient;
            return View(appointments);
        }

        public async Task<IActionResult> MySchedule()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var schedules = await _context.DoctorSchedules
                .Where(s => s.DoctorId == doctor.DoctorId)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            var leaves = await _context.DoctorLeaves
                .Include(l => l.LeaveStatus)
                .Where(l => l.DoctorId == doctor.DoctorId)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            ViewBag.Leaves = leaves;
            return View(schedules);
        }

        public async Task<IActionResult> SubmitLeave()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitLeave(DateOnly startDate, DateOnly endDate, string? reason)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            // Basic date sanity checks before anything touches the DB.
            if (startDate < today)
                ModelState.AddModelError("startDate", "Start date cannot be in the past.");

            if (endDate < startDate)
                ModelState.AddModelError("endDate", "End date must be on or after the start date.");

            if (ModelState.IsValid)
            {
                // Don't allow a new request that overlaps one that's already pending/approved.
                var overlap = await _context.DoctorLeaves
                    .Include(l => l.LeaveStatus)
                    .AnyAsync(l => l.DoctorId == doctor.DoctorId &&
                                   (l.LeaveStatus.LeaveStatus1 == "Pending" ||
                                    l.LeaveStatus.LeaveStatus1 == "Approved") &&
                                   l.StartDate <= endDate &&
                                   l.EndDate >= startDate);

                if (overlap)
                    ModelState.AddModelError("", "You already have a pending or approved leave that overlaps with these dates.");
            }

            if (!ModelState.IsValid)
                return View();

            var pendingStatus = await _context.LeaveStatuses
                .FirstOrDefaultAsync(s => s.LeaveStatus1 == "Pending");

            if (pendingStatus == null)
            {
                TempData["Error"] = "System error: Leave status not configured.";
                return View();
            }

            // New leave always starts Pending - a manager approves or rejects it later.
            var leave = new DoctorLeave
            {
                DoctorId = doctor.DoctorId,
                StartDate = startDate,
                EndDate = endDate,
                Reason = reason,
                LeaveStatusId = pendingStatus.LeaveStatusId
            };

            _context.DoctorLeaves.Add(leave);
            await _context.SaveChangesAsync();

            // Notify every clinic manager so someone picks it up. Look up the role, grab the
            // users in it, then send each one a notification.
            var managerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "ClinicManager");
            if (managerRole != null)
            {
                var managerUserIds = await _context.UserRoles
                    .Where(ur => ur.RoleId == managerRole.Id)
                    .Select(ur => ur.UserId)
                    .ToListAsync();

                var doctorUser = await _userManager.FindByIdAsync(doctor.AspNetUserId);
                var doctorName = doctorUser != null
                    ? $"Dr. {doctorUser.FirstName} {doctorUser.LastName}"
                    : "A doctor";

                foreach (var managerId in managerUserIds)
                {
                    await _notificationService.SendAsync(
                        aspNetUserId: managerId,
                        notificationTypeName: "LeaveRequested",
                        title: "New Leave Request",
                        message: $"{doctorName} has submitted a leave request from " +
                                 $"{startDate:dd MMM yyyy} to {endDate:dd MMM yyyy}." +
                                 (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason}"));
                }
            }

            TempData["Success"] = "Leave request submitted successfully. The clinic manager will review it.";
            return RedirectToAction("MySchedule");
        }

        public async Task<IActionResult> Notifications()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                TempData["Error"] = "Doctor profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var notifications = await _context.Notifications
                .Include(n => n.NotificationType)
                .Where(n => n.AspNetUserId == doctor.AspNetUserId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // Viewing the page marks everything read (only saving if there was anything unread).
            var unread = notifications.Where(n => !n.IsRead).ToList();
            if (unread.Any())
            {
                foreach (var n in unread)
                    n.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(notifications);
        }

        // Sends the right notification(s) after a status change. Not every status is worth
        // a message - the ones not listed in the switches just fall through silently.
        private async Task SendStatusChangeNotificationsAsync(
     Appointment appointment, string newStatus)
        {
            //  Patient notification 
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

                    "Completed" => (
                        "AppointmentCompleted",
                        "Appointment Completed",
                        $"Your appointment on {appointment.ScheduledDate:dd MMM yyyy} has been completed. " +
                            $"Check your prescriptions for any medications issued."),

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

            // Doctor notification (only InProgress for now, as a "consultation started" ping) 
            var doctorProfile = await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.DoctorId == appointment.DoctorId);

            if (doctorProfile != null)
            {
                (string typeName, string title, string message)? doctorPayload = newStatus switch
                {
                    "InProgress" => (
                        "AppointmentConfirmed",
                        "Consultation Started",
                        $"Your appointment on {appointment.ScheduledDate:dd MMM yyyy} at " +
                            $"{appointment.SlotStartTime:HH\\:mm} is now in progress."),

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

        // Pokes the SignalR hub on the API so the waiting-room screen refreshes. Fire and
        // forget - if the API is down the status change has already saved, so just log it.
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

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }
    }
}