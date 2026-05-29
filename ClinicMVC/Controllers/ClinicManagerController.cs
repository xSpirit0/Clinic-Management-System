using ClinicAPI.Models;
using ClinicMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    [Authorize(Roles = "ClinicManager")]// Restrict access to this controller to users in the ClinicManager role
    public class ClinicManagerController : Controller
    {
        // Dependencies for database access, user management, and notification services
        private readonly ClinicDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        // Constructor to inject dependencies for database access, user management, and notification services
        public ClinicManagerController(
            ClinicDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }
        // Redirect to dashboard as the default action for clinic manager
        public IActionResult Index() => RedirectToAction("Dashboard");

        // Clinic manager dashboard showing key metrics and recent activity, with quick links to manage doctors, appointments, and leave requests
        public async Task<IActionResult> Dashboard()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["Error"] = "User profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            ViewBag.TotalDoctors = await _context.DoctorProfiles.CountAsync();
            ViewBag.ActiveDoctors = await _context.DoctorProfiles.CountAsync(d => d.IsActive);
            ViewBag.TotalPatients = await _context.PatientProfiles.CountAsync();
            ViewBag.TodayAppointments = await _context.Appointments.CountAsync(a => a.ScheduledDate == today);
            ViewBag.PendingLeaves = await _context.DoctorLeaves
                .CountAsync(l => l.LeaveStatus.LeaveStatus1 == "Pending");
            ViewBag.TotalAppointments = await _context.Appointments.CountAsync();
            ViewBag.CancelledToday = await _context.Appointments
                .CountAsync(a => a.ScheduledDate == today &&
                            a.AppointmentStatus.AppointmentStatus1 == "Cancelled");

            ViewBag.RecentAppointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor).ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.ScheduledDate == today)
                .OrderBy(a => a.SlotStartTime)
                .Take(5)
                .ToListAsync();

            ViewBag.UnreadNotifications = await _context.Notifications
                .CountAsync(n => n.AspNetUserId == user.Id && !n.IsRead);

            return View();
        }

        // Clinic manager can view a list of all doctors with search capabilities by name or license number, and filter by active/inactive status
        public async Task<IActionResult> Doctors(string? search)
        {
            var query = _context.DoctorProfiles
                .Include(d => d.AspNetUser)
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(d =>
                    (d.AspNetUser != null &&
                     (d.AspNetUser.FirstName.Contains(search) ||
                      d.AspNetUser.LastName.Contains(search))) ||
                    d.LicenseNumber.Contains(search));
            }

            ViewBag.Search = search ?? "";
            return View(await query.OrderBy(d => d.AspNetUser!.FirstName).ToListAsync());
        }

        // Clinic manager can view detailed information about a doctor, including their profile, specializations, schedule, and leave history. This view also provides quick access to manage the doctor's account status, specializations, schedule, and leave requests
        public async Task<IActionResult> DoctorDetails(int id)
        {
            var doctor = await _context.DoctorProfiles
                .Include(d => d.AspNetUser)
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)
                .Include(d => d.DoctorSchedules)
                .Include(d => d.DoctorLeaves)
                    .ThenInclude(l => l.LeaveStatus)
                .FirstOrDefaultAsync(d => d.DoctorId == id);

            if (doctor == null)
            {
                TempData["Error"] = "Doctor not found.";
                return RedirectToAction("Doctors");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            ViewBag.TotalAppointments = await _context.Appointments
                .CountAsync(a => a.DoctorId == id);
            ViewBag.CompletedAppointments = await _context.Appointments
                .CountAsync(a => a.DoctorId == id &&
                            a.AppointmentStatus.AppointmentStatus1 == "Completed");
            ViewBag.UpcomingAppointments = await _context.Appointments
                .CountAsync(a => a.DoctorId == id && a.ScheduledDate >= today);
            ViewBag.AllSpecializations = await _context.Specializations.ToListAsync();
            return View(doctor);
        }
        // Clinic manager can activate or deactivate a doctor's account, but cannot delete it to maintain historical data integrity. Deactivating a doctor will automatically cancel any of their upcoming appointments and notify the patients of the cancellations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDoctorStatus(int id)
        {
            var doctor = await _context.DoctorProfiles.FindAsync(id);
            if (doctor == null)
            {
                TempData["Error"] = "Doctor not found.";
                return RedirectToAction("Doctors");
            }

            doctor.IsActive = !doctor.IsActive;
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                aspNetUserId: doctor.AspNetUserId,
                notificationTypeName: "AccountStatusChanged",
                title: doctor.IsActive ? "Account Activated" : "Account Deactivated",
                message: doctor.IsActive
                    ? "Your doctor account has been activated by the clinic manager."
                    : "Your doctor account has been deactivated by the clinic manager.");

            TempData["Success"] = $"Doctor has been {(doctor.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction("DoctorDetails", new { id });
        }

        // Clinic manager can assign specializations to a doctor, but cannot assign the same specialization multiple times to the same doctor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSpecialization(int doctorId, int specializationId)
        {
            var exists = await _context.DoctorSpecializations
                .AnyAsync(ds => ds.DoctorId == doctorId &&
                           ds.SpecializationId == specializationId);

            if (!exists)
            {
                _context.DoctorSpecializations.Add(new DoctorSpecialization
                {
                    DoctorId = doctorId,
                    SpecializationId = specializationId
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = "Specialization added.";
            }
            else
            {
                TempData["Error"] = "Doctor already has this specialization.";
            }

            return RedirectToAction("DoctorDetails", new { id = doctorId });
        }

        // Clinic manager can remove a specialization from a doctor, but cannot remove all specializations to ensure doctors have at least one area of expertise
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSpecialization(int doctorId, int doctorSpecializationId)
        {
            var ds = await _context.DoctorSpecializations.FindAsync(doctorSpecializationId);
            if (ds != null)
            {
                _context.DoctorSpecializations.Remove(ds);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Specialization removed.";
            }

            return RedirectToAction("DoctorDetails", new { id = doctorId });
        }

        // Clinic manager can view and manage a doctor's weekly schedule, including adding, updating, or deleting schedule days. Any changes to the schedule will automatically cancel any of the doctor's appointments that fall outside the new schedule and notify the patients of the cancellations
        public async Task<IActionResult> ManageSchedule(int doctorId)
        {
            var doctor = await _context.DoctorProfiles
                .Include(d => d.AspNetUser)
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctor == null)
            {
                TempData["Error"] = "Doctor not found.";
                return RedirectToAction("Doctors");
            }

            var schedules = await _context.DoctorSchedules
                .Where(s => s.DoctorId == doctorId)
                .OrderBy(s => s.DayOfWeek)
                .ToListAsync();

            ViewBag.Doctor = doctor;
            ViewBag.Schedules = schedules;
            return View();
        }
        // Clinic manager can add or update a schedule day for a doctor, which will automatically cancel any of the doctor's appointments that fall outside the new schedule and notify the patients of the cancellations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSchedule(
            int doctorId,
            int dayOfWeek,
            TimeOnly startTime,
            TimeOnly endTime,
            int slotDurationMinutes)
        {
            if (startTime >= endTime)
            {
                TempData["Error"] = "Start time must be before end time.";
                return RedirectToAction("ManageSchedule", new { doctorId });
            }

            var existing = await _context.DoctorSchedules
                .FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek);

            if (existing != null)
            {
                existing.StartTime = startTime;
                existing.EndTime = endTime;
                existing.SlotDurationMinutes = slotDurationMinutes;
                existing.IsActive = true;
            }
            else
            {
                _context.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = doctorId,
                    DayOfWeek = dayOfWeek,
                    StartTime = startTime,
                    EndTime = endTime,
                    SlotDurationMinutes = slotDurationMinutes,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Schedule saved successfully.";
            return RedirectToAction("ManageSchedule", new { doctorId });
        }
        // Clinic manager can delete a schedule day, which will automatically cancel any of the doctor's appointments that fall on that day and notify the patients of the cancellations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSchedule(int scheduleId, int doctorId)
        {
            var schedule = await _context.DoctorSchedules.FindAsync(scheduleId);
            if (schedule != null)
            {
                _context.DoctorSchedules.Remove(schedule);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Schedule day removed.";
            }

            return RedirectToAction("ManageSchedule", new { doctorId });
        }

        // View for clinic manager to see all leave requests with filtering options
        public async Task<IActionResult> LeaveRequests(string filter = "pending")
        {
            var query = _context.DoctorLeaves
                .Include(l => l.Doctor).ThenInclude(d => d.AspNetUser)
                .Include(l => l.LeaveStatus)
                .AsQueryable();

            query = filter switch
            {
                "pending" => query.Where(l => l.LeaveStatus.LeaveStatus1 == "Pending"),
                "approved" => query.Where(l => l.LeaveStatus.LeaveStatus1 == "Approved"),
                "rejected" => query.Where(l => l.LeaveStatus.LeaveStatus1 == "Rejected"),
                _ => query.Where(l => l.LeaveStatus.LeaveStatus1 == "Pending")
            };

            ViewBag.Filter = filter;
            ViewBag.PendingCount = await _context.DoctorLeaves
                .CountAsync(l => l.LeaveStatus.LeaveStatus1 == "Pending");

            return View(await query.OrderBy(l => l.StartDate).ToListAsync());
        }
        // Clinic manager can approve leave requests, which will automatically cancel any of the doctor's appointments that fall within the leave period and notify the patients of the cancellations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLeave(int leaveId, string? notes)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["Error"] = "User profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var leave = await _context.DoctorLeaves
                .Include(l => l.LeaveStatus)
                .Include(l => l.Doctor)
                .FirstOrDefaultAsync(l => l.DoctorLeaveId == leaveId);

            if (leave == null)
            {
                TempData["Error"] = "Leave request not found.";
                return RedirectToAction("LeaveRequests");
            }

            var approvedStatus = await _context.LeaveStatuses
                .FirstOrDefaultAsync(s => s.LeaveStatus1 == "Approved");

            if (approvedStatus == null)
            {
                TempData["Error"] = "System error: Approved status not configured.";
                return RedirectToAction("LeaveRequests");
            }

            leave.LeaveStatusId = approvedStatus.LeaveStatusId;
            leave.ApprovedAt = DateTime.Now;
            leave.ApprovedByAspNetUserId = user.Id;
            leave.RejectionReason = null;

            await _context.SaveChangesAsync();

            // Notify the doctor their leave was approved
            await _notificationService.SendAsync(
                aspNetUserId: leave.Doctor?.AspNetUserId,
                notificationTypeName: "LeaveApproved",
                title: "Leave Request Approved",
                message: $"Your leave from {leave.StartDate:dd MMM yyyy} to " +
                         $"{leave.EndDate:dd MMM yyyy} has been approved." +
                         (string.IsNullOrWhiteSpace(notes) ? "" : $" Note: {notes}"));

            // Find and cancel any active appointments during the leave period
            var cancelledStatus = await _context.AppointmentStatuses
                .FirstOrDefaultAsync(s => s.AppointmentStatus1 == "Cancelled");

            if (cancelledStatus != null)
            {
                var affectedAppointments = await _context.Appointments
                    .Include(a => a.AppointmentStatus)
                    .Include(a => a.Patient)
                    .Where(a => a.DoctorId == leave.DoctorId &&
                                a.ScheduledDate >= leave.StartDate &&
                                a.ScheduledDate <= leave.EndDate &&
                                (a.AppointmentStatus.AppointmentStatus1 == "Requested" ||
                                 a.AppointmentStatus.AppointmentStatus1 == "Confirmed"))
                    .ToListAsync();

                foreach (var appointment in affectedAppointments)
                {
                    appointment.AppointmentStatusId = cancelledStatus.AppointmentStatusId;
                    appointment.UpdatedAt = DateTime.Now;

                    _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appointment.AppointmentId,
                        AppointmentStatusId = cancelledStatus.AppointmentStatusId,
                        ChangedAt = DateTime.Now,
                        Notes = "Cancelled due to doctor leave approval.",
                        ChangedByAspNetUserId = user.Id
                    });

                    // Notify the patient their appointment was cancelled
                    if (appointment.Patient?.AspNetUserId != null)
                    {
                        await _notificationService.SendAsync(
                            aspNetUserId: appointment.Patient.AspNetUserId,
                            notificationTypeName: "AppointmentCancelled",
                            title: "Appointment Cancelled",
                            message: $"Your appointment on {appointment.ScheduledDate:dd MMM yyyy} at " +
                                     $"{appointment.SlotStartTime:HH\\:mm} has been cancelled because " +
                                     $"your doctor will be on leave. Please rebook at your convenience.");
                    }
                }

                await _context.SaveChangesAsync();

                if (affectedAppointments.Count > 0)
                    TempData["Success"] = $"Leave approved. {affectedAppointments.Count} appointment(s) were automatically cancelled and patients notified.";
                else
                    TempData["Success"] = "Leave request approved.";
            }
            else
            {
                TempData["Success"] = "Leave request approved.";
            }

            return RedirectToAction("LeaveRequests");
        }
        // Clinic manager can reject leave requests with an optional reason, and the doctor will be notified of the rejection and the reason if provided
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLeave(int leaveId, string rejectionReason)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["Error"] = "User profile not found.";
                return RedirectToAction("Login", "Account");
            }

            var leave = await _context.DoctorLeaves
                .Include(l => l.LeaveStatus)
                .Include(l => l.Doctor)
                .FirstOrDefaultAsync(l => l.DoctorLeaveId == leaveId);

            if (leave == null)
            {
                TempData["Error"] = "Leave request not found.";
                return RedirectToAction("LeaveRequests");
            }

            var rejectedStatus = await _context.LeaveStatuses
                .FirstOrDefaultAsync(s => s.LeaveStatus1 == "Rejected");

            if (rejectedStatus == null)
            {
                TempData["Error"] = "System error: Rejected status not configured.";
                return RedirectToAction("LeaveRequests");
            }

            leave.LeaveStatusId = rejectedStatus.LeaveStatusId;
            leave.RejectionReason = rejectionReason;
            leave.ApprovedAt = null;
            leave.ApprovedByAspNetUserId = user.Id;

            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                aspNetUserId: leave.Doctor?.AspNetUserId,
                notificationTypeName: "LeaveRejected",
                title: "Leave Request Rejected",
                message: $"Your leave from {leave.StartDate:dd MMM yyyy} to " +
                         $"{leave.EndDate:dd MMM yyyy} was rejected." +
                         (string.IsNullOrWhiteSpace(rejectionReason) ? "" : $" Reason: {rejectionReason}"));

            TempData["Success"] = "Leave request rejected.";
            return RedirectToAction("LeaveRequests");
        }

        // View for clinic manager to see all specializations and manage them
        public async Task<IActionResult> Specializations()
        {
            var specs = await _context.Specializations
                .Include(s => s.DoctorSpecializations)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return View(specs);
        }

        // Clinic manager can add new specializations to the system, but cannot edit existing ones to maintain data integrity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNewSpecialization(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Specialization name is required.";
                return RedirectToAction("Specializations");
            }

            var exists = await _context.Specializations
                .AnyAsync(s => s.Name.ToLower() == name.Trim().ToLower());

            if (exists)
            {
                TempData["Error"] = "A specialization with this name already exists.";
                return RedirectToAction("Specializations");
            }

            _context.Specializations.Add(new Specialization
            {
                Name = name.Trim(),
                Description = description?.Trim()
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Specialization '{name}' added.";
            return RedirectToAction("Specializations");
        }

        // Clinic manager can only delete specializations that are not assigned to any doctor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSpecialization(int id)
        {
            var spec = await _context.Specializations
                .Include(s => s.DoctorSpecializations)
                .FirstOrDefaultAsync(s => s.SpecializationId == id);

            if (spec == null)
            {
                TempData["Error"] = "Specialization not found.";
                return RedirectToAction("Specializations");
            }

            if (spec.DoctorSpecializations.Any())
            {
                TempData["Error"] = "Cannot delete a specialization that is assigned to doctors.";
                return RedirectToAction("Specializations");
            }

            _context.Specializations.Remove(spec);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Specialization '{spec.Name}' deleted.";
            return RedirectToAction("Specializations");
        }

        // View for clinic manager to see all patients with search capabilities
        public async Task<IActionResult> Patients(string? search)
        {
            var query = _context.PatientProfiles
                .Include(p => p.AspNetUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(p =>
                    (p.AspNetUser != null &&
                     (p.AspNetUser.FirstName.Contains(search) ||
                      p.AspNetUser.LastName.Contains(search))) ||
                    p.Cprnumber.Contains(search) ||
                    p.PatientReferenceNumber.Contains(search));
            }

            ViewBag.Search = search ?? "";
            return View(await query.OrderBy(p => p.AspNetUser!.FirstName).ToListAsync());
        }

        // View for clinic manager to see all appointments with filtering and search capabilities
        public async Task<IActionResult> AllAppointments(
            string filter = "today",
            string? status = null,
            string? search = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.AspNetUser)
                .Include(a => a.Doctor).ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .AsQueryable();

            query = filter switch
            {
                "today" => query.Where(a => a.ScheduledDate == today),
                "upcoming" => query.Where(a => a.ScheduledDate > today),
                "past" => query.Where(a => a.ScheduledDate < today),
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
                    (a.Doctor.AspNetUser != null &&
                     (a.Doctor.AspNetUser.FirstName.Contains(search) ||
                      a.Doctor.AspNetUser.LastName.Contains(search))));
            }

            ViewBag.Filter = filter;
            ViewBag.Status = status ?? "all";
            ViewBag.Search = search ?? "";
            ViewBag.AllStatuses = await _context.AppointmentStatuses
                .Select(s => s.AppointmentStatus1).ToListAsync();

            return View(await query
                .OrderBy(a => a.ScheduledDate)
                .ThenBy(a => a.SlotStartTime)
                .ToListAsync());
        }

        // View for clinic manager to see all their notifications and mark them as read
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

        // Clinic manager dashboard for managing doctors, receptionists, and other staff accounts
        public async Task<IActionResult> StaffManagement()
        {
            var doctors = await _userManager.GetUsersInRoleAsync("Doctor");
            var receptionists = await _userManager.GetUsersInRoleAsync("Receptionist");

            var doctorProfiles = await _context.DoctorProfiles
                .ToDictionaryAsync(d => d.AspNetUserId ?? "", d => d);

            ViewBag.Doctors = doctors.OrderBy(u => u.FirstName).ToList();
            ViewBag.Receptionists = receptionists.OrderBy(u => u.FirstName).ToList();
            ViewBag.DoctorProfiles = doctorProfiles;
            return View();
        }

        public IActionResult CreateStaff()
        {
            return View();
        }

        // Handle creation of new doctor or receptionist accounts
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(
            string role,
            string firstName,
            string lastName,
            string email,
            string phoneNumber,
            string password,
            string? licenseNumber,
            string? biography)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(phoneNumber))
            {
                TempData["Error"] = "First name, last name, email, phone, and password are required.";
                return View();
            }

            if (role != "Doctor" && role != "Receptionist")
            {
                TempData["Error"] = "Role must be Doctor or Receptionist.";
                return View();
            }

            if (role == "Doctor" && string.IsNullOrWhiteSpace(licenseNumber))
            {
                TempData["Error"] = "License number is required for doctors.";
                return View();
            }

            var existing = await _userManager.FindByEmailAsync(email.Trim());
            if (existing != null)
            {
                TempData["Error"] = "An account with this email already exists.";
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email.Trim(),
                Email = email.Trim(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                PhoneNumber = phoneNumber.Trim(),
                IsActive = true,
                CreatedAt = DateTime.Now,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Could not create account: " +
                    string.Join("; ", result.Errors.Select(e => e.Description));
                return View();
            }

            await _userManager.AddToRoleAsync(user, role);

            if (role == "Doctor")
            {
                _context.DoctorProfiles.Add(new DoctorProfile
                {
                    AspNetUserId = user.Id,
                    LicenseNumber = licenseNumber!.Trim(),
                    Biography = biography?.Trim(),
                    IsActive = true
                });
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"{role} account created for {firstName} {lastName}.";
            return RedirectToAction("StaffManagement");
        }

        // Display edit form for doctor or receptionist
        public async Task<IActionResult> EditStaff(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Staff member not found.";
                return RedirectToAction("StaffManagement");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "";

            DoctorProfile? doctorProfile = null;
            if (role == "Doctor")
                doctorProfile = await _context.DoctorProfiles
                    .FirstOrDefaultAsync(d => d.AspNetUserId == user.Id);

            ViewBag.Role = role;
            ViewBag.DoctorProfile = doctorProfile;
            return View(user);
        }

        // Save edits to doctor or receptionist profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaff(
            string id,
            string firstName,
            string lastName,
            string phoneNumber,
            string? licenseNumber,
            string? biography)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Staff member not found.";
                return RedirectToAction("StaffManagement");
            }

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(phoneNumber))
            {
                TempData["Error"] = "First name, last name, and phone number are required.";
                return RedirectToAction("EditStaff", new { id });
            }

            user.FirstName = firstName.Trim();
            user.LastName = lastName.Trim();
            user.PhoneNumber = phoneNumber.Trim();
            await _userManager.UpdateAsync(user);

            var doctorProfile = await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.AspNetUserId == id);

            if (doctorProfile != null)
            {
                if (!string.IsNullOrWhiteSpace(licenseNumber))
                    doctorProfile.LicenseNumber = licenseNumber.Trim();
                doctorProfile.Biography = biography?.Trim();
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"{firstName} {lastName}'s profile updated.";
            return RedirectToAction("StaffManagement");
        }

        // Toggle active status for doctors and receptionists
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStaffActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Staff member not found.";
                return RedirectToAction("StaffManagement");
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            var doctorProfile = await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.AspNetUserId == id);
            if (doctorProfile != null)
            {
                doctorProfile.IsActive = user.IsActive;
                await _context.SaveChangesAsync();
            }

            await _notificationService.SendAsync(
                aspNetUserId: user.Id,
                notificationTypeName: "AccountStatusChanged",
                title: user.IsActive ? "Account Activated" : "Account Deactivated",
                message: user.IsActive
                    ? "Your account has been activated by the clinic manager."
                    : "Your account has been deactivated by the clinic manager.");

            var name = $"{user.FirstName} {user.LastName}";
            TempData["Success"] = $"{name}'s account has been {(user.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction("StaffManagement");
        }

        /// Helper method to get the currently logged-in user
        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }
    }
}
