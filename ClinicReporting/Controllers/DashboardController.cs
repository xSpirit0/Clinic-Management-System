using ClinicReporting.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ClinicReporting.Controllers
{
    public class DashboardController : Controller
    {
        // Dependency for making HTTP requests to the Clinic API for fetching report data for the dashboard
        private readonly IHttpClientFactory _httpClientFactory;

        // Constructor to inject the IHttpClientFactory dependency, which is used to create HTTP clients for making requests to the Clinic API to fetch report data for the dashboard.
        public DashboardController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Helper method to create an authenticated HttpClient with the JWT token from the session. This client will be used to make API calls to the Clinic API for fetching report data. If the token is not available, it will return a client without authentication, which will likely result in unauthorized responses from the API.
        private HttpClient GetAuthenticatedClient()
        {
            var client = _httpClientFactory.CreateClient("ClinicAPI");
            var token = HttpContext.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        // Helper method to check if the user is authenticated before accessing any dashboard actions. If the user is not authenticated, they will be redirected to the login page.
        private IActionResult? CheckAuth()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "Auth");
            return null;
        }

        // GET: /Dashboard
        // Shows a summary report of the clinic's performance, including total appointments, completed appointments, cancelled appointments, and missed appointments
        public async Task<IActionResult> Index()
        {
            var auth = CheckAuth();
            if (auth != null) return auth;

            var client = GetAuthenticatedClient();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var response = await client.GetAsync("/api/reports/summary");
            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Login", "Auth");

            var json = await response.Content.ReadAsStringAsync();
            var summary = JsonSerializer.Deserialize<SummaryReport>(json, options);

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View(summary);
        }

        // GET: /Dashboard/CancellationRate?from=2024-01-01&to=2024-01-31
        // Shows a report of the number of appointments grouped by their status (completed, cancelled, missed) within a specified date range
        public async Task<IActionResult> AppointmentStats(DateOnly? from, DateOnly? to)
        {
            var auth = CheckAuth();
            if (auth != null) return auth;

            var client = GetAuthenticatedClient();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var url = "/api/reports/appointments/stats";
            var queryParams = new List<string>();
            if (from.HasValue) queryParams.Add($"from={from}");
            if (to.HasValue) queryParams.Add($"to={to}");
            if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Invalid date range. Please make sure 'From' is before 'To'.";
                ViewBag.FullName = HttpContext.Session.GetString("FullName");
                return View(new AppointmentStatsReport());
            }

            var json = await response.Content.ReadAsStringAsync();
            var report = JsonSerializer.Deserialize<AppointmentStatsReport>(json, options);

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View(report);
        }

        // GET: /Dashboard/CancellationRate?from=2024-01-01&to=2024-01-31
        // Shows a report of the cancellation rate of appointments within a specified date range, including the total number of appointments, number of cancelled appointments, and the cancellation rate percentage
        public async Task<IActionResult> CancellationRate(DateOnly? from, DateOnly? to)
        {
            var auth = CheckAuth();
            if (auth != null) return auth;

            var client = GetAuthenticatedClient();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var url = "/api/reports/appointments/cancellation-rate";
            var queryParams = new List<string>();
            if (from.HasValue) queryParams.Add($"from={from}");
            if (to.HasValue) queryParams.Add($"to={to}");
            if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Invalid date range. Please make sure 'From' is before 'To'.";
                ViewBag.FullName = HttpContext.Session.GetString("FullName");
                return View(new CancellationRateReport());
            }

            var json = await response.Content.ReadAsStringAsync();
            var report = JsonSerializer.Deserialize<CancellationRateReport>(json, options);

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View(report);
        }

        // GET: /Dashboard/DoctorUtilization?from=2024-01-01&to=2024-01-31
        //  Shows a report of doctor utilization (number of appointments per doctor) within a specified date range
        public async Task<IActionResult> DoctorUtilization(DateOnly? from, DateOnly? to)
        {
            var auth = CheckAuth();
            if (auth != null) return auth;

            var client = GetAuthenticatedClient();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var url = "/api/reports/doctors/utilization";
            var queryParams = new List<string>();
            if (from.HasValue) queryParams.Add($"from={from}");
            if (to.HasValue) queryParams.Add($"to={to}");
            if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Invalid date range. Please make sure 'From' is before 'To'.";
                ViewBag.FullName = HttpContext.Session.GetString("FullName");
                return View(new DoctorUtilizationReport());
            }

            var json = await response.Content.ReadAsStringAsync();
            var report = JsonSerializer.Deserialize<DoctorUtilizationReport>(json, options);

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View(report);
        }

        // GET: /Dashboard/Specializations?from=2024-01-01&to=2024-01-31
        // Shows a report of the number of appointments for each specialization within a specified date range
        public async Task<IActionResult> Specializations(DateOnly? from, DateOnly? to)
        {
            var auth = CheckAuth();
            if (auth != null) return auth;

            var client = GetAuthenticatedClient();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var url = "/api/reports/specializations/appointments";
            var queryParams = new List<string>();
            if (from.HasValue) queryParams.Add($"from={from}");
            if (to.HasValue) queryParams.Add($"to={to}");
            if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Invalid date range. Please make sure 'From' is before 'To'.";
                ViewBag.FullName = HttpContext.Session.GetString("FullName");
                return View(new SpecializationReport());
            }

            var json = await response.Content.ReadAsStringAsync();
            var report = JsonSerializer.Deserialize<SpecializationReport>(json, options);

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View(report);
        }

        // GET: /Dashboard/MissedAppointments?from=2024-01-01&to=2024-01-31
        // Shows a report of missed appointments within a specified date range
        public async Task<IActionResult> MissedAppointments(DateOnly? from, DateOnly? to)
        {
            var auth = CheckAuth();
            if (auth != null) return auth;

            var client = GetAuthenticatedClient();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var url = "/api/reports/appointments/missed";
            var queryParams = new List<string>();
            if (from.HasValue) queryParams.Add($"from={from}");
            if (to.HasValue) queryParams.Add($"to={to}");
            if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Invalid date range.";
                ViewBag.FullName = HttpContext.Session.GetString("FullName");
                return View(new MissedAppointmentsReport());
            }

            var json = await response.Content.ReadAsStringAsync();
            var report = JsonSerializer.Deserialize<MissedAppointmentsReport>(json, options);

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View(report);
        }

        // GET: /Dashboard/TodayAppointments
        // Shows a report of all appointments scheduled for today, including their status (completed, missed, upcoming)
        public async Task<IActionResult> TodayAppointments()
        {
            var auth = CheckAuth();
            if (auth != null) return auth;

            var client = GetAuthenticatedClient();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var response = await client.GetAsync("/api/reports/appointments/today");
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Could not load today's appointments.";
                ViewBag.FullName = HttpContext.Session.GetString("FullName");
                return View(new TodayAppointmentsReport());
            }

            var json = await response.Content.ReadAsStringAsync();
            var report = JsonSerializer.Deserialize<TodayAppointmentsReport>(json, options);

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View(report);
        }

        // GET: /Dashboard/UpcomingAppointments?days=7
        // Shows a report of all upcoming appointments within the next specified number of days (default is 7), including their scheduled date and time, patient name, doctor name, and status
        public async Task<IActionResult> UpcomingAppointments(int days = 7)
        {
            var auth = CheckAuth();
            if (auth != null) return auth;

            var client = GetAuthenticatedClient();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var response = await client.GetAsync($"/api/reports/appointments/upcoming?days={days}");
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Could not load upcoming appointments.";
                ViewBag.FullName = HttpContext.Session.GetString("FullName");
                return View(new UpcomingAppointmentsReport());
            }

            var json = await response.Content.ReadAsStringAsync();
            var report = JsonSerializer.Deserialize<UpcomingAppointmentsReport>(json, options);

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View(report);
        }
    }
}