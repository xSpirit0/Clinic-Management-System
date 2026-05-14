using ClinicReporting.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace ClinicReporting.Controllers
{
    public class AuthController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("JwtToken") != null)
                return RedirectToAction("Index", "Dashboard");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var client = _httpClientFactory.CreateClient("ClinicAPI");

            var payload = new { email = model.Email, password = model.Password };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/auth/login", content);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Invalid email or password.";
                return View(model);
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var auth = JsonSerializer.Deserialize<AuthResponse>(json, options);

            if (auth == null || !auth.Roles.Contains("ClinicManager"))
            {
                ViewBag.Error = "Access denied. Only Clinic Managers can access this system.";
                return View(model);
            }

            HttpContext.Session.SetString("JwtToken", auth.Token);
            HttpContext.Session.SetString("FullName", auth.FullName);

            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}