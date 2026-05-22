using ClinicAPI.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =====================
// Add services
// =====================
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ClinicDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

builder.Services.AddHttpClient("ClinicApi", client =>
{
    var baseUrl = builder.Configuration["ClinicApi:BaseUrl"]
                  ?? "https://localhost:7221/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// =====================
// Build app (ONLY ONCE)
// =====================
var app = builder.Build();

// =====================
// Configure middleware
// =====================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// =====================
// Routing
// =====================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// =====================
// Run app
// =====================
app.Run();