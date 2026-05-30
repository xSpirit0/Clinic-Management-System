using ClinicAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace ClinicAPI.Services
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ClinicDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await context.Database.MigrateAsync();
            var roles = new[] { "ClinicManager", "Doctor", "Receptionist", "Patient" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }
            if (!context.AppointmentStatuses.Any())
            {
                context.AppointmentStatuses.AddRange(
                    new AppointmentStatus { AppointmentStatus1 = "Requested" },
                    new AppointmentStatus { AppointmentStatus1 = "Confirmed" },
                    new AppointmentStatus { AppointmentStatus1 = "CheckedIn" },
                    new AppointmentStatus { AppointmentStatus1 = "InProgress" },
                    new AppointmentStatus { AppointmentStatus1 = "Completed" },
                    new AppointmentStatus { AppointmentStatus1 = "Cancelled" },
                    new AppointmentStatus { AppointmentStatus1 = "Missed" }
                );
                await context.SaveChangesAsync();
            }
            if (!context.LeaveStatuses.Any())
            {
                context.LeaveStatuses.AddRange(
                    new LeaveStatus { LeaveStatus1 = "Pending" },
                    new LeaveStatus { LeaveStatus1 = "Approved" },
                    new LeaveStatus { LeaveStatus1 = "Rejected" }
                );
                await context.SaveChangesAsync();
            }
            if (!context.PrescriptionStatuses.Any())
            {
                context.PrescriptionStatuses.AddRange(
                    new PrescriptionStatus { PrescriptionStatus1 = "Active" },
                    new PrescriptionStatus { PrescriptionStatus1 = "Completed" },
                    new PrescriptionStatus { PrescriptionStatus1 = "Cancelled" }
                );
                await context.SaveChangesAsync();
            }
            if (!context.NotificationTypes.Any())
            {
                context.NotificationTypes.AddRange(
                    new NotificationType { Type = "AppointmentConfirmed" },
                    new NotificationType { Type = "AppointmentCancelled" },
                    new NotificationType { Type = "AppointmentReminder" },
                    new NotificationType { Type = "AppointmentCompleted" },
                    new NotificationType { Type = "PrescriptionIssued" },
                    new NotificationType { Type = "General" },
                    new NotificationType { Type = "PatientCheckedIn" },
                    new NotificationType { Type = "AppointmentMissed" },
                    new NotificationType { Type = "AccountStatusChanged" },
                    new NotificationType { Type = "LeaveRequested" },
                    new NotificationType { Type = "VisitCompleted" },
                    new NotificationType { Type = "LeaveApproved" },
                    new NotificationType { Type = "LeaveRejected" }
                );
                await context.SaveChangesAsync();
            }
            if (!context.Specializations.Any())
            {
                context.Specializations.AddRange(
                    new Specialization { Name = "General Medicine", Description = "Primary care and general health consultations" },
                    new Specialization { Name = "Cardiology", Description = "Heart and cardiovascular system specialist" },
                    new Specialization { Name = "Dermatology", Description = "Skin, hair, and nail conditions" },
                    new Specialization { Name = "Orthopedics", Description = "Bones, joints, and musculoskeletal system" },
                    new Specialization { Name = "Pediatrics", Description = "Medical care for infants and children" },
                    new Specialization { Name = "Neurology", Description = "Brain and nervous system disorders" }
                );
                await context.SaveChangesAsync();
            }
            async Task<ApplicationUser> EnsureUser(
                string email, string firstName, string lastName, string role)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FirstName = firstName,
                        LastName = lastName,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        EmailConfirmed = true
                    };
                    var result = await userManager.CreateAsync(user, "Pass@1234");
                    if (!result.Succeeded)
                        throw new Exception($"Failed to create user {email}: " +
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                }
                if (!await userManager.IsInRoleAsync(user, role))
                    await userManager.AddToRoleAsync(user, role);
                return user;
            }
            var managerUser = await EnsureUser("manager@spiritclinic.com", "Sarah", "Al-Mansouri", "ClinicManager");
            var doctorUser1 = await EnsureUser("dr.khalid@spiritclinic.com", "Khalid", "Al-Rashidi", "Doctor");
            var doctorUser2 = await EnsureUser("dr.layla@spiritclinic.com", "Layla", "Hassan", "Doctor");
            var receptionistUser = await EnsureUser("reception@spiritclinic.com", "Fatima", "Al-Zahrawi", "Receptionist");
            var patientUser1 = await EnsureUser("ahmed@patient.com", "Ahmed", "Al-Dosari", "Patient");
            var patientUser2 = await EnsureUser("noor@patient.com", "Noor", "Al-Sayed", "Patient");
            if (!context.DoctorProfiles.Any())
            {
                context.DoctorProfiles.AddRange(
                    new DoctorProfile
                    {
                        LicenseNumber = "DOC-2024-001",
                        Biography = "Senior General Practitioner with 12 years of experience in primary care.",
                        IsActive = true,
                        AspNetUserId = doctorUser1.Id
                    },
                    new DoctorProfile
                    {
                        LicenseNumber = "DOC-2024-002",
                        Biography = "Cardiologist specializing in preventive cardiology and heart disease management.",
                        IsActive = true,
                        AspNetUserId = doctorUser2.Id
                    }
                );
                await context.SaveChangesAsync();
            }
            var doctor1 = await context.DoctorProfiles.FirstAsync(d => d.AspNetUserId == doctorUser1.Id);
            var doctor2 = await context.DoctorProfiles.FirstAsync(d => d.AspNetUserId == doctorUser2.Id);
            if (!context.DoctorSpecializations.Any())
            {
                var genMed = await context.Specializations.FirstAsync(s => s.Name == "General Medicine");
                var cardio = await context.Specializations.FirstAsync(s => s.Name == "Cardiology");
                var dermato = await context.Specializations.FirstAsync(s => s.Name == "Dermatology");
                context.DoctorSpecializations.AddRange(
                    new DoctorSpecialization { DoctorId = doctor1.DoctorId, SpecializationId = genMed.SpecializationId },
                    new DoctorSpecialization { DoctorId = doctor1.DoctorId, SpecializationId = dermato.SpecializationId },
                    new DoctorSpecialization { DoctorId = doctor2.DoctorId, SpecializationId = cardio.SpecializationId },
                    new DoctorSpecialization { DoctorId = doctor2.DoctorId, SpecializationId = genMed.SpecializationId }
                );
                await context.SaveChangesAsync();
            }
            if (!context.DoctorSchedules.Any())
            {
                var workDays = new[] { 1, 2, 3, 4, 0 };
                foreach (var day in workDays)
                {
                    context.DoctorSchedules.AddRange(
                        new DoctorSchedule
                        {
                            DoctorId = doctor1.DoctorId,
                            DayOfWeek = day,
                            StartTime = new TimeOnly(8, 0),
                            EndTime = new TimeOnly(16, 0),
                            SlotDurationMinutes = 30
                        },
                        new DoctorSchedule
                        {
                            DoctorId = doctor2.DoctorId,
                            DayOfWeek = day,
                            StartTime = new TimeOnly(9, 0),
                            EndTime = new TimeOnly(17, 0),
                            SlotDurationMinutes = 30
                        }
                    );
                }
                await context.SaveChangesAsync();
            }
            if (!context.PatientProfiles.Any())
            {
                context.PatientProfiles.AddRange(
                    new PatientProfile
                    {
                        AspNetUserId = patientUser1.Id,
                        Cprnumber = "900101001",
                        DateOfBirth = new DateOnly(1990, 1, 1),
                        Gender = "Male",
                        PatientReferenceNumber = "PAT-00001"
                    },
                    new PatientProfile
                    {
                        AspNetUserId = patientUser2.Id,
                        Cprnumber = "950202002",
                        DateOfBirth = new DateOnly(1995, 2, 2),
                        Gender = "Female",
                        PatientReferenceNumber = "PAT-00002"
                    }
                );
                await context.SaveChangesAsync();
            }
            var patient1 = await context.PatientProfiles.FirstAsync(p => p.AspNetUserId == patientUser1.Id);
            var patient2 = await context.PatientProfiles.FirstAsync(p => p.AspNetUserId == patientUser2.Id);
            if (!context.Appointments.Any())
            {
                var confirmedStatus = await context.AppointmentStatuses
                    .FirstAsync(s => s.AppointmentStatus1 == "Confirmed");
                var completedStatus = await context.AppointmentStatuses
                    .FirstAsync(s => s.AppointmentStatus1 == "Completed");
                var cancelledStatus = await context.AppointmentStatuses
                    .FirstAsync(s => s.AppointmentStatus1 == "Cancelled");
                var missedStatus = await context.AppointmentStatuses
                    .FirstAsync(s => s.AppointmentStatus1 == "Missed");
                var requestedStatus = await context.AppointmentStatuses
                    .FirstAsync(s => s.AppointmentStatus1 == "Requested");
                var genMed = await context.Specializations.FirstAsync(s => s.Name == "General Medicine");
                var cardio = await context.Specializations.FirstAsync(s => s.Name == "Cardiology");
                var today = DateOnly.FromDateTime(DateTime.Today);
                context.Appointments.AddRange(
                    new Appointment
                    {
                        PatientId = patient1.PatientId,
                        DoctorId = doctor1.DoctorId,
                        SpecializationId = genMed.SpecializationId,
                        ScheduledDate = today.AddDays(-30),
                        SlotStartTime = new TimeOnly(9, 0),
                        SlotEndTime = new TimeOnly(9, 30),
                        AppointmentStatusId = completedStatus.AppointmentStatusId,
                        ComplaintReason = "Sore throat and mild fever",
                        CreatedAt = DateTime.Now.AddDays(-31),
                        UpdatedAt = DateTime.Now.AddDays(-30)
                    },
                    new Appointment
                    {
                        PatientId = patient2.PatientId,
                        DoctorId = doctor1.DoctorId,
                        SpecializationId = genMed.SpecializationId,
                        ScheduledDate = today.AddDays(-15),
                        SlotStartTime = new TimeOnly(10, 0),
                        SlotEndTime = new TimeOnly(10, 30),
                        AppointmentStatusId = completedStatus.AppointmentStatusId,
                        ComplaintReason = "Lower back pain",
                        CreatedAt = DateTime.Now.AddDays(-16),
                        UpdatedAt = DateTime.Now.AddDays(-15)
                    },
                    new Appointment
                    {
                        PatientId = patient1.PatientId,
                        DoctorId = doctor2.DoctorId,
                        SpecializationId = cardio.SpecializationId,
                        ScheduledDate = today.AddDays(3),
                        SlotStartTime = new TimeOnly(11, 0),
                        SlotEndTime = new TimeOnly(11, 30),
                        AppointmentStatusId = confirmedStatus.AppointmentStatusId,
                        ComplaintReason = "Routine cardiac checkup",
                        CreatedAt = DateTime.Now.AddDays(-2),
                        UpdatedAt = DateTime.Now.AddDays(-2)
                    },
                    new Appointment
                    {
                        PatientId = patient2.PatientId,
                        DoctorId = doctor2.DoctorId,
                        SpecializationId = cardio.SpecializationId,
                        ScheduledDate = today.AddDays(-5),
                        SlotStartTime = new TimeOnly(14, 0),
                        SlotEndTime = new TimeOnly(14, 30),
                        AppointmentStatusId = cancelledStatus.AppointmentStatusId,
                        ComplaintReason = "Palpitations",
                        CreatedAt = DateTime.Now.AddDays(-7),
                        UpdatedAt = DateTime.Now.AddDays(-5)
                    },
                    new Appointment
                    {
                        PatientId = patient1.PatientId,
                        DoctorId = doctor1.DoctorId,
                        SpecializationId = genMed.SpecializationId,
                        ScheduledDate = today,
                        SlotStartTime = new TimeOnly(9, 0),
                        SlotEndTime = new TimeOnly(9, 30),
                        AppointmentStatusId = confirmedStatus.AppointmentStatusId,
                        ComplaintReason = "Follow-up visit",
                        CreatedAt = DateTime.Now.AddDays(-1),
                        UpdatedAt = DateTime.Now.AddDays(-1)
                    },
                    new Appointment
                    {
                        PatientId = patient2.PatientId,
                        DoctorId = doctor1.DoctorId,
                        SpecializationId = genMed.SpecializationId,
                        ScheduledDate = today.AddDays(-20),
                        SlotStartTime = new TimeOnly(13, 0),
                        SlotEndTime = new TimeOnly(13, 30),
                        AppointmentStatusId = missedStatus.AppointmentStatusId,
                        ComplaintReason = "Skin rash",
                        CreatedAt = DateTime.Now.AddDays(-21),
                        UpdatedAt = DateTime.Now.AddDays(-20)
                    }
                );
                await context.SaveChangesAsync();
            }
            if (!context.VisitRecords.Any())
            {
                var activeStatus = await context.PrescriptionStatuses
                    .FirstAsync(s => s.PrescriptionStatus1 == "Active");
                var completed = await context.Appointments
                    .Include(a => a.AppointmentStatus)
                    .Where(a => a.AppointmentStatus.AppointmentStatus1 == "Completed")
                    .ToListAsync();
                foreach (var appt in completed)
                {
                    var visit = new VisitRecord
                    {
                        AppointmentId = appt.AppointmentId,
                        DoctorId = appt.DoctorId,
                        VisitDate = appt.ScheduledDate.ToDateTime(new TimeOnly(10, 0)),
                        Diagnosis = appt.PatientId == 1
                            ? "Acute viral pharyngitis"
                            : "Lumbar muscle strain",
                        Treatment = appt.PatientId == 1
                            ? "Rest, fluids, paracetamol 500mg every 8 hours for 5 days"
                            : "NSAIDs, physiotherapy referral, avoid heavy lifting",
                        DoctorNotes = "Patient presented with mild fever. No signs of bacterial infection."
                    };
                    context.VisitRecords.Add(visit);
                    await context.SaveChangesAsync();
                    var prescription = new Prescription
                    {
                        VisitRecordId = visit.VisitRecordId,
                        PrescriptionStatusId = activeStatus.PrescriptionStatusId,
                        IssuedAt = visit.VisitDate,
                        PrescriptionItems = appt.PatientId == 1
                            ? new List<PrescriptionItem>
                            {
                                new PrescriptionItem {
                                    MedicationName = "Paracetamol",
                                    Dosage         = "500mg",
                                    Frequency      = "Every 8 hours",
                                    DurationDays   = 5,
                                    Instructions   = "Take with water after food"
                                },
                                new PrescriptionItem {
                                    MedicationName = "Loratadine",
                                    Dosage         = "10mg",
                                    Frequency      = "Once daily",
                                    DurationDays   = 7,
                                    Instructions   = "Take in the morning"
                                }
                            }
                            : new List<PrescriptionItem>
                            {
                                new PrescriptionItem {
                                    MedicationName = "Ibuprofen",
                                    Dosage         = "400mg",
                                    Frequency      = "Every 8 hours",
                                    DurationDays   = 5,
                                    Instructions   = "Take with food, avoid on empty stomach"
                                }
                            }
                    };
                    context.Prescriptions.Add(prescription);
                    await context.SaveChangesAsync();
                }
            }
            if (!context.AppointmentStatusHistories.Any())
            {
                var allAppointments = await context.Appointments
                    .Include(a => a.AppointmentStatus)
                    .ToListAsync();
                var requestedStatus = await context.AppointmentStatuses
                    .FirstAsync(s => s.AppointmentStatus1 == "Requested");
                foreach (var appt in allAppointments)
                {
                    context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appt.AppointmentId,
                        AppointmentStatusId = requestedStatus.AppointmentStatusId,
                        ChangedAt = appt.CreatedAt,
                        Notes = "Appointment created"
                    });
                    if (appt.AppointmentStatus.AppointmentStatus1 != "Requested")
                    {
                        context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                        {
                            AppointmentId = appt.AppointmentId,
                            AppointmentStatusId = appt.AppointmentStatusId,
                            ChangedAt = appt.CreatedAt.AddMinutes(30),
                            Notes = $"Status updated to {appt.AppointmentStatus.AppointmentStatus1}"
                        });
                    }
                }
                await context.SaveChangesAsync();
            }
            if (!context.DoctorLeaves.Any())
            {
                var pendingStatus = await context.LeaveStatuses
                    .FirstAsync(s => s.LeaveStatus1 == "Pending");
                var approvedStatus = await context.LeaveStatuses
                    .FirstAsync(s => s.LeaveStatus1 == "Approved");
                var today = DateOnly.FromDateTime(DateTime.Today);
                context.DoctorLeaves.AddRange(
                    new DoctorLeave
                    {
                        DoctorId = doctor1.DoctorId,
                        StartDate = today.AddDays(10),
                        EndDate = today.AddDays(12),
                        Reason = "Medical conference attendance",
                        LeaveStatusId = pendingStatus.LeaveStatusId
                    },
                    new DoctorLeave
                    {
                        DoctorId = doctor2.DoctorId,
                        StartDate = today.AddDays(-10),
                        EndDate = today.AddDays(-8),
                        Reason = "Annual leave",
                        LeaveStatusId = approvedStatus.LeaveStatusId,
                        ApprovedAt = DateTime.Now.AddDays(-12)
                    }
                );
                await context.SaveChangesAsync();
            }
            if (!context.Notifications.Any())
            {
                var confirmedType = await context.NotificationTypes
                    .FirstAsync(n => n.Type == "AppointmentConfirmed");
                var completedType = await context.NotificationTypes
                    .FirstAsync(n => n.Type == "AppointmentCompleted");
                var firstAppt = await context.Appointments.FirstAsync();
                context.Notifications.AddRange(
                    new Notification
                    {
                        NotificationTypeId = confirmedType.NotificationTypeId,
                        AppointmentId = firstAppt.AppointmentId,
                        Title = "Appointment Confirmed",
                        Message = "Your appointment has been confirmed. Please arrive 10 minutes early.",
                        IsRead = false,
                        CreatedAt = DateTime.Now.AddHours(-2),
                        AspNetUserId = patientUser1.Id
                    },
                    new Notification
                    {
                        NotificationTypeId = completedType.NotificationTypeId,
                        AppointmentId = null,
                        Title = "Visit Completed",
                        Message = "Your visit has been completed. A prescription has been issued.",
                        IsRead = true,
                        CreatedAt = DateTime.Now.AddDays(-7),
                        AspNetUserId = patientUser1.Id
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}