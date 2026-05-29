using System.Diagnostics;
using ClinicMVC.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMVC.Controllers
{
    public class HomeController : Controller
    {
        // Dependency for logging
        private readonly ILogger<HomeController> _logger;

        // Constructor to inject the logger dependency
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // This action is used to display the home page of the application. It checks if the user is authenticated and redirects them to their respective dashboard based on their role. If the user is not authenticated, it simply returns the home page view.
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Patient"))
                    return RedirectToAction("Dashboard", "Patient");
                if (User.IsInRole("Doctor"))
                    return RedirectToAction("Dashboard", "Doctor");
                if (User.IsInRole("Receptionist"))
                    return RedirectToAction("Dashboard", "Receptionist");
                if (User.IsInRole("ClinicManager"))
                    return RedirectToAction("Dashboard", "ClinicManager");
            }

            return View();
        }

        // This action is used to display the privacy policy page of the application.
        public IActionResult Privacy()
        {
            return View();
        }
        
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        // This action is used to display the error page when an unhandled exception occurs in the application.
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}