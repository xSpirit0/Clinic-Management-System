using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentStatus",
                columns: table => new
                {
                    AppointmentStatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Appointm__A619B660F625695B", x => x.AppointmentStatusId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveStatus",
                columns: table => new
                {
                    LeaveStatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__LeaveSta__75EE81FAC79A0D52", x => x.LeaveStatusId);
                });

            migrationBuilder.CreateTable(
                name: "NotificationType",
                columns: table => new
                {
                    NotificationTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__299002C1F1C752F7", x => x.NotificationTypeId);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionStatus",
                columns: table => new
                {
                    PrescriptionStatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Prescrip__3FC4F18C9FA63E0C", x => x.PrescriptionStatusId);
                });

            migrationBuilder.CreateTable(
                name: "Specialization",
                columns: table => new
                {
                    SpecializationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Speciali__5809D86FC7E2815F", x => x.SpecializationId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoctorProfile",
                columns: table => new
                {
                    DoctorId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Biography = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AspNetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DoctorPr__2DC00EBF2B67E9C5", x => x.DoctorId);
                    table.ForeignKey(
                        name: "FK_DoctorProfile_AspNetUsers",
                        column: x => x.AspNetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PatientProfile",
                columns: table => new
                {
                    PatientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CPRNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PatientReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BloodType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AspNetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PatientP__970EC366847975D2", x => x.PatientId);
                    table.ForeignKey(
                        name: "FK_PatientProfile_AspNetUsers",
                        column: x => x.AspNetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DoctorLeave",
                columns: table => new
                {
                    DoctorLeaveId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaveStatusId = table.Column<int>(type: "int", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByAspNetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DoctorLe__B1CB912B446FF000", x => x.DoctorLeaveId);
                    table.ForeignKey(
                        name: "FK_DoctorLeave_ApprovedByAspNetUsers",
                        column: x => x.ApprovedByAspNetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DoctorLeave_Doctor",
                        column: x => x.DoctorId,
                        principalTable: "DoctorProfile",
                        principalColumn: "DoctorId");
                    table.ForeignKey(
                        name: "FK_DoctorLeave_Status",
                        column: x => x.LeaveStatusId,
                        principalTable: "LeaveStatus",
                        principalColumn: "LeaveStatusId");
                });

            migrationBuilder.CreateTable(
                name: "DoctorSchedule",
                columns: table => new
                {
                    ScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DoctorSc__9C8A5B49BD74EEC9", x => x.ScheduleId);
                    table.ForeignKey(
                        name: "FK_DoctorSchedule_Doctor",
                        column: x => x.DoctorId,
                        principalTable: "DoctorProfile",
                        principalColumn: "DoctorId");
                });

            migrationBuilder.CreateTable(
                name: "DoctorSpecialization",
                columns: table => new
                {
                    DoctorSpecializationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    SpecializationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DoctorSp__14F6ED4D30E323BD", x => x.DoctorSpecializationId);
                    table.ForeignKey(
                        name: "FK_DoctorSpecialization_Doctor",
                        column: x => x.DoctorId,
                        principalTable: "DoctorProfile",
                        principalColumn: "DoctorId");
                    table.ForeignKey(
                        name: "FK_DoctorSpecialization_Specialization",
                        column: x => x.SpecializationId,
                        principalTable: "Specialization",
                        principalColumn: "SpecializationId");
                });

            migrationBuilder.CreateTable(
                name: "Appointment",
                columns: table => new
                {
                    AppointmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    SpecializationId = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SlotStartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    SlotEndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    AppointmentStatusId = table.Column<int>(type: "int", nullable: false),
                    ComplaintReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysdatetime())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByAspNetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Appointm__8ECDFCC234F9E7BB", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_Appointment_CreatedByAspNetUsers",
                        column: x => x.CreatedByAspNetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointment_Doctor",
                        column: x => x.DoctorId,
                        principalTable: "DoctorProfile",
                        principalColumn: "DoctorId");
                    table.ForeignKey(
                        name: "FK_Appointment_Patient",
                        column: x => x.PatientId,
                        principalTable: "PatientProfile",
                        principalColumn: "PatientId");
                    table.ForeignKey(
                        name: "FK_Appointment_Specialization",
                        column: x => x.SpecializationId,
                        principalTable: "Specialization",
                        principalColumn: "SpecializationId");
                    table.ForeignKey(
                        name: "FK_Appointment_Status",
                        column: x => x.AppointmentStatusId,
                        principalTable: "AppointmentStatus",
                        principalColumn: "AppointmentStatusId");
                });

            migrationBuilder.CreateTable(
                name: "AppointmentStatusHistory",
                columns: table => new
                {
                    AppointmentStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    AppointmentStatusId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysdatetime())"),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedByAspNetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Appointm__BD119A9D10F52473", x => x.AppointmentStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_StatusHistory_Appointment",
                        column: x => x.AppointmentId,
                        principalTable: "Appointment",
                        principalColumn: "AppointmentId");
                    table.ForeignKey(
                        name: "FK_StatusHistory_ChangedByAspNetUsers",
                        column: x => x.ChangedByAspNetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StatusHistory_Status",
                        column: x => x.AppointmentStatusId,
                        principalTable: "AppointmentStatus",
                        principalColumn: "AppointmentStatusId");
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationTypeId = table.Column<int>(type: "int", nullable: false),
                    AppointmentId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysdatetime())"),
                    AspNetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__20CF2E126219CA94", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notification_Appointment",
                        column: x => x.AppointmentId,
                        principalTable: "Appointment",
                        principalColumn: "AppointmentId");
                    table.ForeignKey(
                        name: "FK_Notification_AspNetUsers",
                        column: x => x.AspNetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_Type",
                        column: x => x.NotificationTypeId,
                        principalTable: "NotificationType",
                        principalColumn: "NotificationTypeId");
                });

            migrationBuilder.CreateTable(
                name: "VisitRecord",
                columns: table => new
                {
                    VisitRecordId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    DoctorNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Diagnosis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Treatment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VisitDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__VisitRec__922FA65C66223DFF", x => x.VisitRecordId);
                    table.ForeignKey(
                        name: "FK_VisitRecord_Appointment",
                        column: x => x.AppointmentId,
                        principalTable: "Appointment",
                        principalColumn: "AppointmentId");
                    table.ForeignKey(
                        name: "FK_VisitRecord_Doctor",
                        column: x => x.DoctorId,
                        principalTable: "DoctorProfile",
                        principalColumn: "DoctorId");
                });

            migrationBuilder.CreateTable(
                name: "Prescription",
                columns: table => new
                {
                    PrescriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitRecordId = table.Column<int>(type: "int", nullable: false),
                    PrescriptionStatusId = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Prescrip__401308327312BAF5", x => x.PrescriptionId);
                    table.ForeignKey(
                        name: "FK_Prescription_Status",
                        column: x => x.PrescriptionStatusId,
                        principalTable: "PrescriptionStatus",
                        principalColumn: "PrescriptionStatusId");
                    table.ForeignKey(
                        name: "FK_Prescription_VisitRecord",
                        column: x => x.VisitRecordId,
                        principalTable: "VisitRecord",
                        principalColumn: "VisitRecordId");
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItem",
                columns: table => new
                {
                    PrescriptionItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionId = table.Column<int>(type: "int", nullable: false),
                    MedicationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Prescrip__1AADD9FA83ADD7FE", x => x.PrescriptionItemId);
                    table.ForeignKey(
                        name: "FK_PrescriptionItem_Prescription",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescription",
                        principalColumn: "PrescriptionId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_AppointmentStatusId",
                table: "Appointment",
                column: "AppointmentStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_CreatedByAspNetUserId",
                table: "Appointment",
                column: "CreatedByAspNetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_DoctorId",
                table: "Appointment",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_PatientId",
                table: "Appointment",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointment_SpecializationId",
                table: "Appointment",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "UQ__Appointm__BABC6966ECC57928",
                table: "AppointmentStatus",
                column: "AppointmentStatus",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStatusHistory_AppointmentId",
                table: "AppointmentStatusHistory",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStatusHistory_AppointmentStatusId",
                table: "AppointmentStatusHistory",
                column: "AppointmentStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStatusHistory_ChangedByAspNetUserId",
                table: "AppointmentStatusHistory",
                column: "ChangedByAspNetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLeave_ApprovedByAspNetUserId",
                table: "DoctorLeave",
                column: "ApprovedByAspNetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLeave_DoctorId",
                table: "DoctorLeave",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorLeave_LeaveStatusId",
                table: "DoctorLeave",
                column: "LeaveStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorProfile_AspNetUserId",
                table: "DoctorProfile",
                column: "AspNetUserId");

            migrationBuilder.CreateIndex(
                name: "UQ__DoctorPr__E88901666FFDCB7D",
                table: "DoctorProfile",
                column: "LicenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedule_DoctorId",
                table: "DoctorSchedule",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecialization_SpecializationId",
                table: "DoctorSpecialization",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "UQ_DoctorSpecialization",
                table: "DoctorSpecialization",
                columns: new[] { "DoctorId", "SpecializationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__LeaveSta__8A6C54D169C428B9",
                table: "LeaveStatus",
                column: "LeaveStatus",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_AppointmentId",
                table: "Notification",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_AspNetUserId",
                table: "Notification",
                column: "AspNetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_NotificationTypeId",
                table: "Notification",
                column: "NotificationTypeId");

            migrationBuilder.CreateIndex(
                name: "UQ__Notifica__F9B8A48BC37A2A65",
                table: "NotificationType",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientProfile_AspNetUserId",
                table: "PatientProfile",
                column: "AspNetUserId");

            migrationBuilder.CreateIndex(
                name: "UQ__PatientP__8C7D9721B58AD64C",
                table: "PatientProfile",
                column: "PatientReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__PatientP__BD136F743CE5CFA6",
                table: "PatientProfile",
                column: "CPRNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescription_PrescriptionStatusId",
                table: "Prescription",
                column: "PrescriptionStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescription_VisitRecordId",
                table: "Prescription",
                column: "VisitRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItem_PrescriptionId",
                table: "PrescriptionItem",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "UQ__Prescrip__5F9A9EEFA8E95B53",
                table: "PrescriptionStatus",
                column: "PrescriptionStatus",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Speciali__737584F62ABE0C44",
                table: "Specialization",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitRecord_DoctorId",
                table: "VisitRecord",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "UQ__VisitRec__8ECDFCC3027BE4FD",
                table: "VisitRecord",
                column: "AppointmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentStatusHistory");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DoctorLeave");

            migrationBuilder.DropTable(
                name: "DoctorSchedule");

            migrationBuilder.DropTable(
                name: "DoctorSpecialization");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "PrescriptionItem");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "LeaveStatus");

            migrationBuilder.DropTable(
                name: "NotificationType");

            migrationBuilder.DropTable(
                name: "Prescription");

            migrationBuilder.DropTable(
                name: "PrescriptionStatus");

            migrationBuilder.DropTable(
                name: "VisitRecord");

            migrationBuilder.DropTable(
                name: "Appointment");

            migrationBuilder.DropTable(
                name: "DoctorProfile");

            migrationBuilder.DropTable(
                name: "PatientProfile");

            migrationBuilder.DropTable(
                name: "Specialization");

            migrationBuilder.DropTable(
                name: "AppointmentStatus");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
