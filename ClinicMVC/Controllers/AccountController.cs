using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using ClinicMVC.ViewModels;
using ClinicAPI.Models;

namespace ClinicMVC.Controllers
{
    public class AccountController : Controller
    {
        // UserManager is used to manage users in the system
        private readonly UserManager<ApplicationUser> _userManager;
        // SignInManager is used for signing users in and out
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ClinicDbContext _context;
        // Constructor to inject UserManager and SignInManager
        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ClinicDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        // Show the registration page
        // GET: /Account/Register 
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // Handle registration form submission
        // POST: /Account/Register 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Check if the form data is valid
            if (ModelState.IsValid)
            {
                // Create a new user with the email and password from the form
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber
                };
              

                // Try to create the user in the database
                var result = await _userManager.CreateAsync(user, model.Password);
             
                
                
                    // If user creation was successful
                    if (result.Succeeded)
                    {
                    var createdUser = await _userManager.FindByEmailAsync(model.Email);
                    Console.WriteLine($"User creation result: {result.Succeeded}, User ID: {createdUser?.Id}");
                    if (createdUser == null)
                    {
                        ModelState.AddModelError(string.Empty, "An error occurred while creating your account. Please try again.");
                        return View(model);
                    }
                    // Add the user to the "Patient" role
                    await _userManager.AddToRoleAsync(user, "Patient");

                        var patientProfile = new PatientProfile
                        {
                            Cprnumber = model.CprNumber,
                            PatientReferenceNumber = "PAT-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                            DateOfBirth = model.DateOfBirth,
                            Gender = model.Gender,
                            AspNetUserId = createdUser.Id
                        };
                        try
                        {
                            _context.PatientProfiles.Add(patientProfile);
                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error saving patient profile: {ex.Message}");
                            // If there was an error saving the patient profile, delete the user and show an error
                            await _userManager.DeleteAsync(user);
                            ModelState.AddModelError(string.Empty, "An error occurred while creating your profile. Please try again.");
                              return View(model);
                        }
                    
                    // Sign the user in
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    // Redirect to the Patient dashboard
                    return RedirectToAction("Dashboard", "Patient");
                }

                // If there were errors, add them to the ModelState
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            // something failed, redisplay form
            return View(model);
        }

        // Show the login page
        // GET: /Account/Login 
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // Handle login form submission
        // POST: /Account/Login 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Check if the form data is valid
            if (ModelState.IsValid)
            {
                // Try to sign in the user with the provided credentials
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

                // If login was successful
                if (result.Succeeded)
                {
                    // Find the user by email
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (user != null)
                    {
                        // Check the user's role and redirect to the correct dashboard
                        if (await _userManager.IsInRoleAsync(user, "ClinicManager"))
                        {
                            return RedirectToAction("Dashboard", "ClinicManager");
                        }
                        if (await _userManager.IsInRoleAsync(user, "Doctor"))
                        {
                            return RedirectToAction("Dashboard", "Doctor");
                        }
                        if (await _userManager.IsInRoleAsync(user, "Patient"))
                        {
                            return RedirectToAction("Dashboard", "Patient");
                        }
                        if (await _userManager.IsInRoleAsync(user, "Receptionist"))
                        {
                            return RedirectToAction("Dashboard", "Receptionist");
                        }
                        // If user has no known role, go to home page
                        return RedirectToAction("Index", "Home");
                    }
                }

                // If the account is locked out
                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty,
                        "Account locked out. Try again later.");
                }
                else
                {
                    // If login failed, show error
                    ModelState.AddModelError(string.Empty,
                        "Invalid email or password.");
                }
            }
            // If we got this far, something failed, redisplay form
            return View(model);
        }

        // Handle user logout
        // POST: /Account/Logout 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Sign the user out
            await _signInManager.SignOutAsync();
            // Redirect to the home page
            return RedirectToAction("Index", "Home");
        }

        // Show access denied page
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
