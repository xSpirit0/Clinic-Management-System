using ClinicAPI.Models;
using ClinicAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container, including controllers, database context, Identity for authentication, Swagger for API documentation, and SignalR for real-time communication. The code configures JWT authentication and CORS policies to allow the MVC frontend to connect to the API's SignalR hub.
builder.Services.AddControllers();

builder.Services.AddDbContext<ClinicDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(
               Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Configure Identity services for user authentication and roles, with custom password settings
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>()
.AddSignInManager()
.AddEntityFrameworkStores<ClinicDbContext>();

// Add services for API documentation and testing with Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger to support JWT authentication in the API documentation and testing interface
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste only the JWT token. Swagger will add Bearer automatically."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Register the token service for generating JWT tokens
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),

            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
    });

// SignalR
builder.Services.AddSignalR();

// CORS - allow the MVC frontend's browser to connect to the API's Hub
builder.Services.AddCors(options =>
{
    options.AddPolicy("MvcClientPolicy", policy =>
    {
        policy.WithOrigins(
                "https://localhost:7056",  // ClinicMVC HTTPS
                "http://localhost:5205")   // ClinicMVC HTTP
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();         
    });
});

// Build the application and configure the middleware pipeline
var app = builder.Build();
app.UseCors("MvcClientPolicy");

// Enable Swagger in development environment for API documentation and testing
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
     app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{

    await SeedData.InitializeAsync(scope.ServiceProvider);
}
// If not in development, use custom error page and HSTS for security
app.UseHttpsRedirection();

app.UseAuthentication(); 
app.UseAuthorization();
// Map controller routes and SignalR hubs
app.MapControllers();
app.MapHub<ClinicAPI.Hubs.WaitingRoomHub>("/hubs/waitingroom");
app.Run();