using ClinicAPI.Models;
using Microsoft.EntityFrameworkCore;
//using Microsoft.AspNetCore.Identity;

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
app.UseAuthentication();
app.UseAuthorization();

// =====================
// Routing
// =====================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);
app.MapRazorPages();


// =====================
// Run app
// =====================
app.Run();