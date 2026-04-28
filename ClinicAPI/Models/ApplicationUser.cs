using Microsoft.AspNetCore.Identity;

namespace ClinicAPI.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
    }
}
