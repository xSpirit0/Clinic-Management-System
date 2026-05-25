using ClinicAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =====================
// Add services
// =====================

// Add MVC controllers and views to the service container
builder.Services.AddControllersWithViews();

// Add the database context and configure it to use SQL Server
builder.Services.AddDbContext<ClinicDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// Add Identity services for user authentication and roles
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    // Password settings for user accounts
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    // Lockout settings for failed login attempts
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddRoles<IdentityRole>()                        // Add support for roles
    .AddEntityFrameworkStores<ClinicDbContext>();    // Use EF Core for Identity

// Configure the application cookie (for login and access denied paths)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Named HttpClient for calling the API (Public Lookup + SignalR broadcast trigger)
builder.Services.AddHttpClient("ClinicApi", client =>
{
    var baseUrl = builder.Configuration["ClinicApi:BaseUrl"]
                  ?? "https://localhost:7221/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Generic HttpClient registration (kept for any unnamed factory usage)
builder.Services.AddHttpClient();

// =====================
// Build app (ONLY ONCE)
// =====================
var app = builder.Build();

// =====================
// Seed roles and demo users on startup
// =====================
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    // List of roles to create if they don't exist
    string[] roles = { "ClinicManager", "Patient", "Doctor", "Receptionist" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    // Seed ClinicManager user
    var adminEmail = "clinicManager@Clinic.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            FirstName = "Ali",
            LastName = "Ahmed",
            Email = adminEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(adminUser, "Admin1234@");
        await userManager.AddToRoleAsync(adminUser, "ClinicManager");
    }

    // Seed Doctor user
    var doctorEmail = "doctor1@Clinic.com";
    var doctorUser = await userManager.FindByEmailAsync(doctorEmail);
    if (doctorUser == null)
    {
        doctorUser = new ApplicationUser
        {
            UserName = doctorEmail,
            FirstName = "Dr. Abbas",
            LastName = "Hasan",
            Email = doctorEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(doctorUser, "Doctor1234@");
        await userManager.AddToRoleAsync(doctorUser, "Doctor");
    }

    // Seed Patient user
    var patientEmail = "zahraa@gmail.com";
    var patientUser = await userManager.FindByEmailAsync(patientEmail);
    if (patientUser == null)
    {
        patientUser = new ApplicationUser
        {
            UserName = patientEmail,
            FirstName = "Zahraa",
            LastName = "Humaidan",
            Email = patientEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(patientUser, "Patient1234@");
        await userManager.AddToRoleAsync(patientUser, "Patient");
    }

    // Seed Receptionist user
    var receptionistEmail = "receptionist1@Clinic.com";
    var receptionistUser = await userManager.FindByEmailAsync(receptionistEmail);
    if (receptionistUser == null)
    {
        receptionistUser = new ApplicationUser
        {
            UserName = receptionistEmail,
            FirstName = "Sara",
            LastName = "Ali",
            Email = receptionistEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(receptionistUser, "Receptionist1234@");
        await userManager.AddToRoleAsync(receptionistUser, "Receptionist");
    }
}

// =====================
// Configure middleware
// =====================

// If not in development, use custom error page and HSTS for security
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Redirect HTTP requests to HTTPS
app.UseHttpsRedirection();

// Serve static files (CSS, JS, images, signalr.js, etc.)
app.UseStaticFiles();

// Add routing middleware
app.UseRouting();

// Authentication MUST come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// =====================
// Routing
// =====================

// Default MVC route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// Razor Pages endpoints (needed for Identity scaffolded pages)
app.MapRazorPages();

// =====================
// Run app
// =====================
app.Run();