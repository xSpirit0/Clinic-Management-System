using ClinicAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicAPI.Services
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.MigrateAsync();

            // Roles
            if (!await roleManager.RoleExistsAsync("ClinicManager"))
            {
                await roleManager.CreateAsync(new IdentityRole("ClinicManager"));
            }

            // User
            var adminEmail = "admin@clinic.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                await userManager.CreateAsync(adminUser, "123456");
                await userManager.AddToRoleAsync(adminUser, "ClinicManager");
            }

            // Specializations
            if (!context.Specializations.Any())
            {
                context.Specializations.AddRange(
                    new Specialization { Name = "Cardiology", Description = "Heart specialist" },
                    new Specialization { Name = "Dermatology", Description = "Skin specialist" },
                    new Specialization { Name = "General Medicine", Description = "General doctor" }
                );
                await context.SaveChangesAsync();
            }

            // Doctor
            if (!context.DoctorProfiles.Any())
            {
                context.DoctorProfiles.Add(new DoctorProfile
                {
                    LicenseNumber = "DOC-001",
                    Biography = "Senior doctor",
                    IsActive = true,
                    AspNetUserId = adminUser.Id 
                });
                await context.SaveChangesAsync();
            }

            // Patient
            if (!context.PatientProfiles.Any())
            {
                context.PatientProfiles.Add(new PatientProfile
                {
                    Cprnumber = "123456789",
                    PatientReferenceNumber = "PAT-001",
                    DateOfBirth = new DateOnly(2000, 1, 1),
                    Gender = "Male",
                    BloodType = "O+"
                });
                await context.SaveChangesAsync();
            }

            // Appointment Status
            if (!context.AppointmentStatuses.Any())
            {
                context.AppointmentStatuses.AddRange(
                    new AppointmentStatus { AppointmentStatus1 = "Confirmed" },
                    new AppointmentStatus { AppointmentStatus1 = "Completed" },
                    new AppointmentStatus { AppointmentStatus1 = "Cancelled" },
                    new AppointmentStatus { AppointmentStatus1 = "Missed" }
                );
                await context.SaveChangesAsync();
            }

            // Appointments
            if (!context.Appointments.Any())
            {
                var doctor = context.DoctorProfiles.First();
                var patient = context.PatientProfiles.First();
                var specs = context.Specializations.ToList();
                var statuses = context.AppointmentStatuses.ToList();

                context.Appointments.AddRange(
                    new Appointment
                    {
                        PatientId = patient.PatientId,
                        DoctorId = doctor.DoctorId,
                        SpecializationId = specs[0].SpecializationId,
                        ScheduledDate = DateOnly.FromDateTime(DateTime.Today),
                        SlotStartTime = new TimeOnly(9, 0),
                        SlotEndTime = new TimeOnly(9, 30),
                        AppointmentStatusId = statuses.First(s => s.AppointmentStatus1 == "Confirmed").AppointmentStatusId,
                        CreatedAt = DateTime.Now
                    },
                    new Appointment
                    {
                        PatientId = patient.PatientId,
                        DoctorId = doctor.DoctorId,
                        SpecializationId = specs[1].SpecializationId,
                        ScheduledDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                        SlotStartTime = new TimeOnly(10, 0),
                        SlotEndTime = new TimeOnly(10, 30),
                        AppointmentStatusId = statuses.First(s => s.AppointmentStatus1 == "Completed").AppointmentStatusId,
                        CreatedAt = DateTime.Now
                    },
                    new Appointment
                    {
                        PatientId = patient.PatientId,
                        DoctorId = doctor.DoctorId,
                        SpecializationId = specs[2].SpecializationId,
                        ScheduledDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                        SlotStartTime = new TimeOnly(11, 0),
                        SlotEndTime = new TimeOnly(11, 30),
                        AppointmentStatusId = statuses.First(s => s.AppointmentStatus1 == "Cancelled").AppointmentStatusId,
                        CreatedAt = DateTime.Now
                    },
                    new Appointment
                    {
                        PatientId = patient.PatientId,
                        DoctorId = doctor.DoctorId,
                        SpecializationId = specs[0].SpecializationId,
                        ScheduledDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                        SlotStartTime = new TimeOnly(12, 0),
                        SlotEndTime = new TimeOnly(12, 30),
                        AppointmentStatusId = statuses.First(s => s.AppointmentStatus1 == "Missed").AppointmentStatusId,
                        CreatedAt = DateTime.Now
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}