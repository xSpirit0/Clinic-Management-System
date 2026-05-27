using ClinicAPI.Models;
using ClinicMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{

        public class ClinicManagerController : Controller
        {
            private readonly ClinicDbContext _context;
          private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ClinicManagerController(ClinicDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
            {
                _context = context;
                _userManager = userManager;
                _roleManager = roleManager;
            }

            public IActionResult Index() => RedirectToAction("Dashboard");

            // DASHBOARD 
            public async Task<IActionResult> Dashboard()
            {
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

                return View();
            }

            //  DOCTORS LIST
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

            // DOCTOR DETAILS 
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

            //TOGGLE DOCTOR ACTIVE STATUS 
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

                TempData["Success"] = $"Doctor has been {(doctor.IsActive ? "activated" : "deactivated")}.";
                return RedirectToAction("DoctorDetails", new { id });
            }

            //  MANAGE DOCTOR SPECIALIZATIONS 
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

            // SCHEDULE MANAGEMENT
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

                // Check for duplicate day entry
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

            // LEAVE REQUESTS 
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

            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> ApproveLeave(int leaveId, string? notes)
            {
                var leave = await _context.DoctorLeaves
                    .Include(l => l.LeaveStatus)
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
                leave.ApprovedByAspNetUserId = null;
                leave.RejectionReason = null;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Leave request approved.";
                return RedirectToAction("LeaveRequests");
            }

            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> RejectLeave(int leaveId, string rejectionReason)
            {
                var leave = await _context.DoctorLeaves
                    .Include(l => l.LeaveStatus)
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

                await _context.SaveChangesAsync();
                TempData["Success"] = "Leave request rejected.";
                return RedirectToAction("LeaveRequests");
            }

            // SPECIALIZATIONS 
            public async Task<IActionResult> Specializations()
            {
                var specs = await _context.Specializations
                    .Include(s => s.DoctorSpecializations)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                return View(specs);
            }

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

            // PATIENTS LIST
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

            //ALL APPOINTMENTS
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
        // ADD NEW USER VIEW 
        [HttpGet]
        public IActionResult AddNewUser()
        {
            ViewBag.Specializations = _context.Specializations.Select(s => new SelectListItem
            {
                Value = s.SpecializationId.ToString(),
                Text = s.Name
            }).ToList();
            return View();
        }
        //POST: ADD NEW Doctor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNewUser(UserViewModel model)
        {
            // Check if the form data is valid
            if (ModelState.IsValid)
            {
                // Create a new user with the email and password from the form
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                };


                // Try to create the user in the database
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    var createdUser = await _userManager.FindByEmailAsync(model.Email);
                    Console.WriteLine($"User creation result: {result.Succeeded}, User ID: {createdUser?.Id}");
                    if (createdUser == null)
                    {
                        ModelState.AddModelError(string.Empty, "An error occurred while creating your account. Please try again.");
                        return View(model);
                    }
                    // Add the user to the specified role
                    await _userManager.AddToRoleAsync(user, model.Role);
                    // if reciptionist show success message
                    if (model.Role == "Receptionist")
                    {
                        TempData["SuccessMessage"] = "Receptionist account created successfully.";
                        return RedirectToAction("AddNewUser");
                    }

                    // create a doctor profile if the role is doctor, and link it to the created user
                    if (model.Role == "Doctor")
                    {
                        if (model.SpecializationId == null)
                        {
                            ModelState.AddModelError(string.Empty, "Please select a specialization for the doctor.");
                            return View(model);
                        }
                        if (model.LicenseNumber == null)
                        {
                            ModelState.AddModelError(string.Empty, "Please enter a license number for the doctor.");
                            return View(model);
                        }
                        var doctorProfile = new DoctorProfile
                        {
                            LicenseNumber = model.LicenseNumber,
                            DoctorSpecializations = new List<DoctorSpecialization>
                            {

                                new DoctorSpecialization { SpecializationId = model.SpecializationId.Value }
                            },
                            AspNetUserId = createdUser.Id
                        };
                        try
                        {
                            _context.DoctorProfiles.Add(doctorProfile);
                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error saving patient profile: {ex.Message}");
                            // If there was an error saving the patient profile, delete the user and show an error
                            await _userManager.DeleteAsync(user);
                            ModelState.AddModelError(string.Empty, "An error occurred while creating your profile. Please try again.");
                            return View(model);
                        }
                    }
                    return RedirectToAction("DoctorDetails", "ClinicManager");
                }

                // If there were errors, add them to the ModelState
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            // something failed, redisplay form
            return View(model);
        }


    }
}
      