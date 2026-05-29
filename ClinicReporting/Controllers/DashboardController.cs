using ClinicReporting.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ClinicReporting.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DashboardController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private HttpClient GetAuthenticatedClient()
        {
            var client = _httpClientFactory.CreateClient("ClinicAPI");
            var token = HttpContext.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private IActionResult? CheckAuth()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "Auth");
            return null;
        }

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