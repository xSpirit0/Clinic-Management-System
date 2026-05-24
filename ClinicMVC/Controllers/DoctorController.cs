using ClinicAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicMVC.Controllers
{
    
    public class DoctorController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public DoctorController(
            ClinicDbContext context,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        private const int TempDoctorId = 1;

        
        public async Task<IActionResult> Dashboard()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            
            var todayAppointments = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.DoctorId == TempDoctorId &&
                            a.ScheduledDate == today)
                .OrderBy(a => a.SlotStartTime)
                .ToListAsync();

            
            var totalToday = todayAppointments.Count;
            var completedToday = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "Completed");
            var inProgressNow = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "InProgress");
            var pendingToday = todayAppointments
                .Count(a => a.AppointmentStatus.AppointmentStatus1 == "Requested" ||
                            a.AppointmentStatus.AppointmentStatus1 == "Confirmed" ||
                            a.AppointmentStatus.AppointmentStatus1 == "CheckedIn");

            
            var upcomingWeek = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.DoctorId == TempDoctorId &&
                            a.ScheduledDate > today &&
                            a.ScheduledDate <= today.AddDays(7) &&
                            a.AppointmentStatus.AppointmentStatus1 != "Cancelled" &&
                            a.AppointmentStatus.AppointmentStatus1 != "Missed")
                .OrderBy(a => a.ScheduledDate)
                .ThenBy(a => a.SlotStartTime)
                .ToListAsync();

            ViewBag.TodayAppointments = todayAppointments;
            ViewBag.UpcomingWeek = upcomingWeek;
            ViewBag.TotalToday = totalToday;
            ViewBag.CompletedToday = completedToday;
            ViewBag.InProgressNow = inProgressNow;
            ViewBag.PendingToday = pendingToday;

            return View();
        }

        
        public async Task<IActionResult> MyAppointments(string filter = "today", string? status = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a => a.DoctorId == TempDoctorId);

            
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

            ViewBag.CurrentFilter = filter;
            ViewBag.CurrentStatus = status ?? "all";

          
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
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                    .ThenInclude(v => v != null ? v.Prescriptions : null)
                        .ThenInclude(p => p.PrescriptionItems)
                .Include(a => a.AppointmentStatusHistories.OrderByDescending(h => h.ChangedAt))
                    .ThenInclude(h => h.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                          a.DoctorId == TempDoctorId);

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
            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .FirstOrDefaultAsync(a => a.AppointmentId == id &&
                                          a.DoctorId == TempDoctorId);

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

            var currentStatus = appointment.AppointmentStatus.AppointmentStatus1;
            if (!IsValidTransition(currentStatus, newStatus))
            {
                TempData["Error"] = $"Cannot transition from '{currentStatus}' to '{newStatus}'.";
                return RedirectToAction("AppointmentDetails", new { id });
            }

            appointment.AppointmentStatusId = newStatusEntity.AppointmentStatusId;
            appointment.UpdatedAt = DateTime.Now;

            var history = new AppointmentStatusHistory
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentStatusId = newStatusEntity.AppointmentStatusId,
                ChangedAt = DateTime.Now,
                Notes = notes,
                ChangedByAspNetUserId = null
            };
            _context.AppointmentStatusHistories.Add(history);

            await _context.SaveChangesAsync();
            await NotifyWaitingRoomAsync();

            TempData["Success"] = $"Appointment status updated to '{newStatus}'.";
            return RedirectToAction("AppointmentDetails", new { id });
        }

       
        private bool IsValidTransition(string from, string to)
        {
            if (to == "Cancelled" || to == "Missed") return true;

            return (from, to) switch
            {
                ("Requested", "Confirmed") => true,
                ("Confirmed", "CheckedIn") => true,
                ("CheckedIn", "InProgress") => true,
                ("InProgress", "Completed") => true,
                _ => false
            };
        }

        public async Task<IActionResult> WriteVisitRecord(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId &&
                                          a.DoctorId == TempDoctorId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

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
            var appointment = await _context.Appointments
                .Include(a => a.AppointmentStatus)
                .Include(a => a.VisitRecord)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId &&
                                          a.DoctorId == TempDoctorId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

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
                    DoctorId = TempDoctorId,
                    DoctorNotes = doctorNotes,
                    Diagnosis = diagnosis,
                    Treatment = treatment,
                    VisitDate = DateTime.Now
                };
                _context.VisitRecords.Add(visit);
            }

           
            if (appointment.AppointmentStatus.AppointmentStatus1 != "Completed")
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
                        ChangedByAspNetUserId = null
                    });
                }
            }

            await _context.SaveChangesAsync();
            await NotifyWaitingRoomAsync();

            TempData["Success"] = "Visit record saved.";
            return RedirectToAction("AppointmentDetails", new { id = appointmentId });
        }

        
        public async Task<IActionResult> AddPrescription(int visitRecordId)
        {
            var visit = await _context.VisitRecords
                .Include(v => v.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.AspNetUser)
                .Include(v => v.Prescriptions)
                    .ThenInclude(p => p.PrescriptionItems)
                .FirstOrDefaultAsync(v => v.VisitRecordId == visitRecordId &&
                                          v.DoctorId == TempDoctorId);

            if (visit == null)
            {
                TempData["Error"] = "Visit record not found or not yours.";
                return RedirectToAction("MyAppointments");
            }

            return View(visit);
        }

        // ==================== ADD PRESCRIPTION (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescription(int visitRecordId,
                                                         List<string> medicationName,
                                                         List<string> dosage,
                                                         List<string> frequency,
                                                         List<int?> durationDays,
                                                         List<string?> instructions)
        {
            var visit = await _context.VisitRecords
                .FirstOrDefaultAsync(v => v.VisitRecordId == visitRecordId &&
                                          v.DoctorId == TempDoctorId);

            if (visit == null)
            {
                TempData["Error"] = "Visit record not found.";
                return RedirectToAction("MyAppointments");
            }

            // Filter out empty rows
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


            TempData["Success"] = $"Prescription with {validIndexes.Count} medication(s) added.";
            return RedirectToAction("AppointmentDetails",
                new { id = visit.AppointmentId });
        }

        // ==================== MY PATIENTS ====================
        public async Task<IActionResult> MyPatients()
        {
            var patients = await _context.Appointments
                .Where(a => a.DoctorId == TempDoctorId)
                .Include(a => a.Patient)
                    .ThenInclude(p => p.AspNetUser)
                .Select(a => a.Patient)
                .Distinct()
                .OrderBy(p => p.AspNetUser!.FirstName)
                .ThenBy(p => p.AspNetUser!.LastName)
                .ToListAsync();

            return View(patients);
        }

        // ==================== PATIENT HISTORY ====================
        public async Task<IActionResult> PatientHistory(int id)
        {
            // Verify this patient has had at least one appointment with me
            var hasSeen = await _context.Appointments
                .AnyAsync(a => a.PatientId == id && a.DoctorId == TempDoctorId);

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

        // ==================== MY SCHEDULE ====================
        public async Task<IActionResult> MySchedule()
        {
            var schedules = await _context.DoctorSchedules
                .Where(s => s.DoctorId == TempDoctorId)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            // Approved leave for the next 60 days
            var today = DateOnly.FromDateTime(DateTime.Today);
            var leaves = await _context.DoctorLeaves
                .Include(l => l.LeaveStatus)
                .Where(l => l.DoctorId == TempDoctorId &&
                            l.LeaveStatus.LeaveStatus1 == "Approved" &&
                            l.EndDate >= today)
                .OrderBy(l => l.StartDate)
                .ToListAsync();

            ViewBag.Leaves = leaves;
            return View(schedules);
        }

       
        public async Task<IActionResult> Notifications()
        {
           
            var notifications = new List<Notification>();
            return View(notifications);
        }

        
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

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }
    }
}
