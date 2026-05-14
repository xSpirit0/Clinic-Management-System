namespace ClinicReporting.Models
{
    public class LoginViewModel
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class AuthResponse
    {
        public string Token { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public List<string> Roles { get; set; } = new();
    }
}