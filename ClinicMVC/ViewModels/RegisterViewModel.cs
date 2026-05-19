using System.ComponentModel.DataAnnotations;
namespace ClinicMVC.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        [StringLength(50, ErrorMessage = "First name cannot be longer than {1} characters.")]
        public required string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        [StringLength(50, ErrorMessage = "Last name cannot be longer than {1} characters.")]
        public required string LastName { get; set; }

        [Required]
        [Display(Name = "Username")]
        [StringLength(30, MinimumLength = 4, ErrorMessage = "Username must be between {2} and {1} characters.")]
        public required string UserName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public required string Email { get; set; }

        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        [StringLength(20, ErrorMessage = "Phone number cannot be longer than {1} characters.")]
        public required string PhoneNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8,
            ErrorMessage = "Password must be at least {2} characters long.")]
        [Display(Name = "Password")]
        public required string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password",
            ErrorMessage = "The password and confirmation password do not match.")]
        [Display(Name = "Confirm Password")]
        public required string ConfirmPassword { get; set; }
    }
}
