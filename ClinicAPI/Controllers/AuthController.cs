using ClinicAPI.DTOs;
using ClinicAPI.Models;
using ClinicAPI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClinicAPI.Controllers;

// This controller handles authentication-related actions such as user login. It uses ASP.NET Core Identity for user management and JWT for token generation.
[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    // Dependencies for user management, sign-in management, token generation, configuration, and role management.
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;
    private readonly RoleManager<IdentityRole> _roleManager;

    // Constructor to inject dependencies for user management, sign-in management, token generation, configuration, and role management.
    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IConfiguration config,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _config = config;
        _roleManager = roleManager;
    }

    // POST: /api/auth/login
    // This endpoint allows users to log in by providing their email and password.
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(
            user, dto.Password, lockoutOnFailure: false);

        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var token = await _tokenService.CreateTokenAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        // Return the token and user info to the client
        return Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:ExpiryMinutes"]!)),
            Email = user.Email ?? "",
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Roles = roles
        });
    }
}