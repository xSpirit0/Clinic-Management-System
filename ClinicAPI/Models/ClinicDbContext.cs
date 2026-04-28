using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClinicAPI.Models;

public partial class ClinicDbContext : IdentityDbContext<ApplicationUser>
{
    public ClinicDbContext()
    {
    }

    public ClinicDbContext(DbContextOptions<ClinicDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Appointment> Appointments { get; set; }
    public virtual DbSet<AppointmentStatus> AppointmentStatuses { get; set; }
    public virtual DbSet<AppointmentStatusHistory> AppointmentStatusHistories { get; set; }
    public virtual DbSet<DoctorLeave> DoctorLeaves { get; set; }
    public virtual DbSet<DoctorProfile> DoctorProfiles { get; set; }
    public virtual DbSet<DoctorSchedule> DoctorSchedules { get; set; }
    public virtual DbSet<DoctorSpecialization> DoctorSpecializations { get; set; }
    public virtual DbSet<LeaveStatus> LeaveStatuses { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<NotificationType> NotificationTypes { get; set; }
    public virtual DbSet<PatientProfile> PatientProfiles { get; set; }
    public virtual DbSet<Prescription> Prescriptions { get; set; }
    public virtual DbSet<PrescriptionItem> PrescriptionItems { get; set; }
    public virtual DbSet<PrescriptionStatus> PrescriptionStatuses { get; set; }
    public virtual DbSet<Specialization> Specializations { get; set; }
    public virtual DbSet<VisitRecord> VisitRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__8ECDFCC234F9E7BB");
            entity.ToTable("Appointment");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.CreatedByAspNetUserId).HasMaxLength(450);

            entity.HasOne(d => d.AppointmentStatus).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.AppointmentStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointment_Status");

            entity.HasOne(d => d.CreatedByAspNetUser).WithMany()
                .HasForeignKey(d => d.CreatedByAspNetUserId)
                .HasConstraintName("FK_Appointment_CreatedByAspNetUsers");

            entity.HasOne(d => d.Doctor).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointment_Doctor");

            entity.HasOne(d => d.Patient).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointment_Patient");

            entity.HasOne(d => d.Specialization).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.SpecializationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointment_Specialization");
        });

        modelBuilder.Entity<AppointmentStatus>(entity =>
        {
            entity.HasKey(e => e.AppointmentStatusId).HasName("PK__Appointm__A619B660F625695B");
            entity.ToTable("AppointmentStatus");
            entity.HasIndex(e => e.AppointmentStatus1, "UQ__Appointm__BABC6966ECC57928").IsUnique();
            entity.Property(e => e.AppointmentStatus1).HasMaxLength(50).HasColumnName("AppointmentStatus");
        });

        modelBuilder.Entity<AppointmentStatusHistory>(entity =>
        {
            entity.HasKey(e => e.AppointmentStatusHistoryId).HasName("PK__Appointm__BD119A9D10F52473");
            entity.ToTable("AppointmentStatusHistory");

            entity.Property(e => e.ChangedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.ChangedByAspNetUserId).HasMaxLength(450);

            entity.HasOne(d => d.Appointment).WithMany(p => p.AppointmentStatusHistories)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StatusHistory_Appointment");

            entity.HasOne(d => d.AppointmentStatus).WithMany(p => p.AppointmentStatusHistories)
                .HasForeignKey(d => d.AppointmentStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StatusHistory_Status");

            entity.HasOne(d => d.ChangedByAspNetUser).WithMany()
                .HasForeignKey(d => d.ChangedByAspNetUserId)
                .HasConstraintName("FK_StatusHistory_ChangedByAspNetUsers");
        });

        modelBuilder.Entity<DoctorLeave>(entity =>
        {
            entity.HasKey(e => e.DoctorLeaveId).HasName("PK__DoctorLe__B1CB912B446FF000");
            entity.ToTable("DoctorLeave");

            entity.Property(e => e.ApprovedByAspNetUserId).HasMaxLength(450);

            entity.HasOne(d => d.ApprovedByAspNetUser).WithMany()
                .HasForeignKey(d => d.ApprovedByAspNetUserId)
                .HasConstraintName("FK_DoctorLeave_ApprovedByAspNetUsers");

            entity.HasOne(d => d.Doctor).WithMany(p => p.DoctorLeaves)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DoctorLeave_Doctor");

            entity.HasOne(d => d.LeaveStatus).WithMany(p => p.DoctorLeaves)
                .HasForeignKey(d => d.LeaveStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DoctorLeave_Status");
        });

        modelBuilder.Entity<DoctorProfile>(entity =>
        {
            entity.HasKey(e => e.DoctorId).HasName("PK__DoctorPr__2DC00EBF2B67E9C5");
            entity.ToTable("DoctorProfile");

            entity.HasIndex(e => e.LicenseNumber, "UQ__DoctorPr__E88901666FFDCB7D").IsUnique();

            entity.Property(e => e.AspNetUserId).HasMaxLength(450);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LicenseNumber).HasMaxLength(50);

            entity.HasOne(d => d.AspNetUser).WithMany()
                .HasForeignKey(d => d.AspNetUserId)
                .HasConstraintName("FK_DoctorProfile_AspNetUsers");
        });

        modelBuilder.Entity<DoctorSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("PK__DoctorSc__9C8A5B49BD74EEC9");
            entity.ToTable("DoctorSchedule");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SlotDurationMinutes).HasDefaultValue(30);

            entity.HasOne(d => d.Doctor).WithMany(p => p.DoctorSchedules)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DoctorSchedule_Doctor");
        });

        modelBuilder.Entity<DoctorSpecialization>(entity =>
        {
            entity.HasKey(e => e.DoctorSpecializationId).HasName("PK__DoctorSp__14F6ED4D30E323BD");
            entity.ToTable("DoctorSpecialization");

            entity.HasIndex(e => new { e.DoctorId, e.SpecializationId }, "UQ_DoctorSpecialization").IsUnique();

            entity.HasOne(d => d.Doctor).WithMany(p => p.DoctorSpecializations)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DoctorSpecialization_Doctor");

            entity.HasOne(d => d.Specialization).WithMany(p => p.DoctorSpecializations)
                .HasForeignKey(d => d.SpecializationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DoctorSpecialization_Specialization");
        });

        modelBuilder.Entity<LeaveStatus>(entity =>
        {
            entity.HasKey(e => e.LeaveStatusId).HasName("PK__LeaveSta__75EE81FAC79A0D52");
            entity.ToTable("LeaveStatus");
            entity.HasIndex(e => e.LeaveStatus1, "UQ__LeaveSta__8A6C54D169C428B9").IsUnique();
            entity.Property(e => e.LeaveStatus1).HasMaxLength(50).HasColumnName("LeaveStatus");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E126219CA94");
            entity.ToTable("Notification");

            entity.Property(e => e.AspNetUserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Appointment).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_Notification_Appointment");

            entity.HasOne(d => d.AspNetUser).WithMany()
                .HasForeignKey(d => d.AspNetUserId)
                .HasConstraintName("FK_Notification_AspNetUsers");

            entity.HasOne(d => d.NotificationType).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.NotificationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_Type");
        });

        modelBuilder.Entity<NotificationType>(entity =>
        {
            entity.HasKey(e => e.NotificationTypeId).HasName("PK__Notifica__299002C1F1C752F7");
            entity.ToTable("NotificationType");
            entity.HasIndex(e => e.Type, "UQ__Notifica__F9B8A48BC37A2A65").IsUnique();
            entity.Property(e => e.Type).HasMaxLength(100);
        });

        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.HasKey(e => e.PatientId).HasName("PK__PatientP__970EC366847975D2");
            entity.ToTable("PatientProfile");

            entity.HasIndex(e => e.PatientReferenceNumber, "UQ__PatientP__8C7D9721B58AD64C").IsUnique();
            entity.HasIndex(e => e.Cprnumber, "UQ__PatientP__BD136F743CE5CFA6").IsUnique();

            entity.Property(e => e.AspNetUserId).HasMaxLength(450);
            entity.Property(e => e.BloodType).HasMaxLength(10);
            entity.Property(e => e.Cprnumber).HasMaxLength(20).HasColumnName("CPRNumber");
            entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactPhone).HasMaxLength(30);
            entity.Property(e => e.Gender).HasMaxLength(20);
            entity.Property(e => e.PatientReferenceNumber).HasMaxLength(50);

            entity.HasOne(d => d.AspNetUser).WithMany()
                .HasForeignKey(d => d.AspNetUserId)
                .HasConstraintName("FK_PatientProfile_AspNetUsers");
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => e.PrescriptionId).HasName("PK__Prescrip__401308327312BAF5");
            entity.ToTable("Prescription");

            entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.PrescriptionStatus).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.PrescriptionStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescription_Status");

            entity.HasOne(d => d.VisitRecord).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.VisitRecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescription_VisitRecord");
        });

        modelBuilder.Entity<PrescriptionItem>(entity =>
        {
            entity.HasKey(e => e.PrescriptionItemId).HasName("PK__Prescrip__1AADD9FA83ADD7FE");
            entity.ToTable("PrescriptionItem");

            entity.Property(e => e.Dosage).HasMaxLength(100);
            entity.Property(e => e.Frequency).HasMaxLength(100);
            entity.Property(e => e.MedicationName).HasMaxLength(150);

            entity.HasOne(d => d.Prescription).WithMany(p => p.PrescriptionItems)
                .HasForeignKey(d => d.PrescriptionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PrescriptionItem_Prescription");
        });

        modelBuilder.Entity<PrescriptionStatus>(entity =>
        {
            entity.HasKey(e => e.PrescriptionStatusId).HasName("PK__Prescrip__3FC4F18C9FA63E0C");
            entity.ToTable("PrescriptionStatus");
            entity.HasIndex(e => e.PrescriptionStatus1, "UQ__Prescrip__5F9A9EEFA8E95B53").IsUnique();
            entity.Property(e => e.PrescriptionStatus1).HasMaxLength(50).HasColumnName("PrescriptionStatus");
        });

        modelBuilder.Entity<Specialization>(entity =>
        {
            entity.HasKey(e => e.SpecializationId).HasName("PK__Speciali__5809D86FC7E2815F");
            entity.ToTable("Specialization");
            entity.HasIndex(e => e.Name, "UQ__Speciali__737584F62ABE0C44").IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<VisitRecord>(entity =>
        {
            entity.HasKey(e => e.VisitRecordId).HasName("PK__VisitRec__922FA65C66223DFF");
            entity.ToTable("VisitRecord");

            entity.HasIndex(e => e.AppointmentId, "UQ__VisitRec__8ECDFCC3027BE4FD").IsUnique();
            entity.Property(e => e.VisitDate).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Appointment).WithOne(p => p.VisitRecord)
                .HasForeignKey<VisitRecord>(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitRecord_Appointment");

            entity.HasOne(d => d.Doctor).WithMany(p => p.VisitRecords)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VisitRecord_Doctor");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}