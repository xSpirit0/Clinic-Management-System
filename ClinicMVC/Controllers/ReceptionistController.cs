using ClinicAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    
    public class ReceptionistController : Controller
    {
        private readonly ClinicDbContext _context;

       
        public ReceptionistController(ClinicDbContext context)
        {
            _context = context;
        }

        //  DASHBOARD 
        public async Task<IActionResult> Dashboard()
        {
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

            var totalToday     = todayAppointments.Count;
            var pendingToday   = todayAppointments.Count(a =>
                a.AppointmentStatus.AppointmentStatus1 == "Requested" ||
                a.AppointmentStatus.AppointmentStatus1 == "Confirmed");
            var checkedIn      = todayAppointments.Count(a =>
                a.AppointmentStatus.AppointmentStatus1 == "CheckedIn" ||
                a.AppointmentStatus.AppointmentStatus1 == "InProgress");
            var completedToday = todayAppointments.Count(a =>
                a.AppointmentStatus.AppointmentStatus1 == "Completed");

            ViewBag.TodayAppointments = todayAppointments;
            ViewBag.TotalToday        = totalToday;
            ViewBag.PendingToday      = pendingToday;
            ViewBag.CheckedIn         = checkedIn;
            ViewBag.CompletedToday    = completedToday;

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
                "today"    => query.Where(a => a.ScheduledDate == today),
                "upcoming" => query.Where(a => a.ScheduledDate > today),
                "past"     => query.Where(a => a.ScheduledDate < today),
                "all"      => query,
                _          => query.Where(a => a.ScheduledDate == today)
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
            ViewBag.Search        = search ?? "";
            ViewBag.AllStatuses   = await _context.AppointmentStatuses
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
            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
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
                AppointmentId        = appointment.AppointmentId,
                AppointmentStatusId  = newStatusEntity.AppointmentStatusId,
                ChangedAt            = DateTime.Now,
                Notes                = notes,
                ChangedByAspNetUserId = null 
            });

            await _context.SaveChangesAsync();

          

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
                ("Requested",  "Confirmed") => true,
                ("Confirmed",  "CheckedIn") => true,
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
                    id   = ds.Doctor.DoctorId,
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
                        endTime   = slotEnd.ToString(),
                        display   = current.ToString("HH\\:mm") + " - " + slotEnd.ToString("HH\\:mm")
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
            
            var patient = await _context.PatientProfiles
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
                PatientId            = patientId,
                DoctorId             = doctorId,
                SpecializationId     = specializationId,
                ScheduledDate        = scheduledDate,
                SlotStartTime        = slotStartTime,
                SlotEndTime          = slotEndTime,
                AppointmentStatusId  = confirmedStatus.AppointmentStatusId,
                ComplaintReason      = complaintReason,
                CreatedByAspNetUserId = null, 
                CreatedAt            = DateTime.Now,
                UpdatedAt            = DateTime.Now
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // TODO: Send notification to doctor and patient 

            TempData["Success"] =
                $"Appointment booked successfully for {scheduledDate:dd MMM yyyy} at {slotStartTime:HH\\:mm}.";

            return RedirectToAction("PatientProfile", new { id = patientId });
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }
    }
}
