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