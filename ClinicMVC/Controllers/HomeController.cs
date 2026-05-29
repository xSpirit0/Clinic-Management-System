using System.Diagnostics;
using ClinicMVC.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

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

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}