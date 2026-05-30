var builder = WebApplication.CreateBuilder(args);

// Add MVC controllers and views to the service container
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

// Add the database context and configure it to use SQL Server
builder.Services.AddHttpClient("ClinicAPI", client =>
{
    var url = builder.Configuration["SpiritClinicApi:BaseUrl"]
              ?? builder.Configuration["ClinicApi:BaseUrl"]
              ?? "https://api-spiritclinic-s5g4-ss-dkf8c3hhgncpgsgy.ukwest-01.azurewebsites.net/";
    client.BaseAddress = new Uri(url);
});

// Build the application
var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}")
    .WithStaticAssets();
// Run the application
app.Run();
