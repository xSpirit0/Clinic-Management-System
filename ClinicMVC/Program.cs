using ClinicAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ClinicMVC.Services;
// This is the main entry point for the ClinicMVC application. It sets up the web application, configures services, and defines the middleware pipeline. The code uses the minimal hosting model introduced in .NET 6, which simplifies the setup of ASP.NET Core applications. The application is configured to use MVC controllers with views, Entity Framework Core for database access, and ASP.NET Identity for authentication and authorization. Additionally, it registers a custom notification service and configures an HttpClient for making API calls to a clinic API.
var builder = WebApplication.CreateBuilder(args);


// Add MVC controllers and views to the service container
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<INotificationService, NotificationService>();

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
    .AddRoles<IdentityRole>()                        
    .AddEntityFrameworkStores<ClinicDbContext>();     

// Configure the application cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.Cookie.MaxAge = null;
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

// Build the application
var app = builder.Build();



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


// Default MVC route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// Razor Pages endpoints (needed for Identity scaffolded pages)
app.MapRazorPages();

//  Run the application
app.Run();