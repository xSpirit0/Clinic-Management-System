using System.Text.Json;
using ClinicMVC.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMVC.Controllers
{
    
    public class PublicLookupController : Controller
    {
        // Dependencies for making HTTP requests and logging
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PublicLookupController> _logger;

        // Constructor to inject dependencies
        public PublicLookupController(
            IHttpClientFactory httpClientFactory,
            ILogger<PublicLookupController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        //GET: search form 
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // POST: handle search 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(string cprNumber, string referenceNumber)
        {
            // Basic validation to ensure both fields are filled out
            if (string.IsNullOrWhiteSpace(cprNumber) ||
                string.IsNullOrWhiteSpace(referenceNumber))
            {
                ViewBag.Error = "Please enter both your CPR number and reference number.";
                return View("Index");
            }

           
            cprNumber = cprNumber.Trim();
            referenceNumber = referenceNumber.Trim();

            try
            {
               
                var client = _httpClientFactory.CreateClient("ClinicApi");

                
                var url = $"api/public/patient-lookup" +
                          $"?cprNumber={Uri.EscapeDataString(cprNumber)}" +
                          $"&referenceNumber={Uri.EscapeDataString(referenceNumber)}";

                var response = await client.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    ViewBag.Error = "No matching patient found. " +
                                    "Please check your CPR number and reference number.";
                    ViewBag.LastCpr = cprNumber;
                    ViewBag.LastRef = referenceNumber;
                    return View("Index");
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("API returned non-success status: {Status}",
                                       response.StatusCode);
                    ViewBag.Error = "We couldn't reach the lookup service right now. " +
                                    "Please try again in a few minutes.";
                    return View("Index");
                }

                // Parse the JSON response
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<PublicLookupResult>(json, options);

                if (result == null)
                {
                    ViewBag.Error = "Could not read the lookup result. Please try again.";
                    return View("Index");
                }

                return View("Results", result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request to lookup API failed");
                ViewBag.Error = "We couldn't connect to the lookup service. " +
                                "Please make sure you're online and try again.";
                return View("Index");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Lookup API request timed out");
                ViewBag.Error = "The lookup service is slow to respond. Please try again.";
                return View("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during patient lookup");
                ViewBag.Error = "Something went wrong. Please try again later.";
                return View("Index");
            }
        }
    }
}
