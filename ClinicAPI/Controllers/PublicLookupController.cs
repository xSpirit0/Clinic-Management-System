using ClinicAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace ClinicAPI.Controllers
{
    // This controller provides public endpoints for looking up patient information, upcoming appointments, and recent visit records based on a patient's CPR number and reference number. It allows users to access this information without requiring authentication, making it useful for patients who want to quickly check their details without logging in.
    [AllowAnonymous]
    [Route("api/public")]
    [ApiController]
    public class PublicLookupController : ControllerBase
    {
        // Dependency for interacting with the database to retrieve patient information, appointments, and visit records based on the provided CPR number and reference number.
        private readonly ClinicDbContext _context;

        // Constructor to inject the ClinicDbContext dependency, which is used to interact with the database for retrieving patient information, appointments, and visit records based on the provided CPR number and reference number.
        public PublicLookupController(ClinicDbContext context)
        {
            _context = context;
        }

        // GET: api/public/patient-lookup?cprNumber=123456-7890&referenceNumber=REF123
        // This endpoint allows public users to look up patient information using their CPR number and reference number. It returns the patient's name, upcoming appointments, and recent visit records without requiring authentication.
        [HttpGet("patient-lookup")]
        public async Task<IActionResult> GetPatientData(
            string cprNumber,
            string referenceNumber)
        {
            var patient = await _context.PatientProfiles
                .Include(p => p.AspNetUser)
                .FirstOrDefaultAsync(p =>
                    p.Cprnumber == cprNumber &&
                    p.PatientReferenceNumber == referenceNumber);
            // If no patient is found with the provided CPR number and reference number, return a 404 Not Found response with a message indicating that the patient was not found.
            if (patient == null)
                return NotFound(new { message = "Patient not found. Please check your CPR number and reference number." });

            var upcomingAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(a => a.Specialization)
                .Include(a => a.AppointmentStatus)
                .Where(a =>
                    a.PatientId == patient.PatientId &&
                    a.ScheduledDate >= DateOnly.FromDateTime(DateTime.Today))
                .OrderBy(a => a.ScheduledDate)
                .ThenBy(a => a.SlotStartTime)
                .Select(a => new
                {
                    a.AppointmentId,
                    ScheduledDate = a.ScheduledDate.ToString("yyyy-MM-dd"),
                    SlotStartTime = a.SlotStartTime.ToString("HH\\:mm"),
                    SlotEndTime = a.SlotEndTime.ToString("HH\\:mm"),
                    DoctorName = a.Doctor.AspNetUser != null
                        ? "Dr. " + a.Doctor.AspNetUser.FirstName + " " + a.Doctor.AspNetUser.LastName
                        : "Unknown Doctor",
                    Specialization = a.Specialization.Name,
                    Status = a.AppointmentStatus.AppointmentStatus1,
                    a.ComplaintReason
                })
                .ToListAsync();
            // Retrieve the 5 most recent visit records for the patient, including details about the doctor, specialization, diagnosis, treatment, and prescription count. The results are ordered by visit date in descending order.
            var recentVisits = await _context.VisitRecords
                .Include(v => v.Doctor)
                    .ThenInclude(d => d.AspNetUser)
                .Include(v => v.Appointment)
                    .ThenInclude(a => a.Specialization)
                .Include(v => v.Prescriptions)
                    .ThenInclude(p => p.PrescriptionItems)
                .Where(v => v.Appointment.PatientId == patient.PatientId)
                .OrderByDescending(v => v.VisitDate)
                .Take(5)
                .Select(v => new
                {
                    v.VisitRecordId,
                    VisitDate = v.VisitDate.ToString("yyyy-MM-dd HH\\:mm"),
                    DoctorName = v.Doctor.AspNetUser != null
                        ? "Dr. " + v.Doctor.AspNetUser.FirstName + " " + v.Doctor.AspNetUser.LastName
                        : "Unknown Doctor",
                    Specialization = v.Appointment.Specialization != null
                        ? v.Appointment.Specialization.Name
                        : "—",
                    v.Diagnosis,
                    v.Treatment,
                    PrescriptionCount = v.Prescriptions.Count
                })
                .ToListAsync();

            return Ok(new
            {
                PatientId = patient.PatientId,
                PatientName = (patient.AspNetUser?.FirstName ?? "") + " " + (patient.AspNetUser?.LastName ?? ""),
                PatientReferenceNumber = patient.PatientReferenceNumber,
                UpcomingAppointments = upcomingAppointments,
                RecentVisits = recentVisits
            });
        }
    }
}