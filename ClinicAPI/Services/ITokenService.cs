using ClinicAPI.Models;

namespace ClinicAPI.Services;

public interface ITokenService
{
    Task<string> CreateTokenAsync(ApplicationUser user);
}