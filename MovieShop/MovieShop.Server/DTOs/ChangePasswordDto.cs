using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.DTOs
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "The current password is required.")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "Entering a new password is mandatory.")]
        [MinLength(8, ErrorMessage = "The new password must be at least 8 characters long.")]
        public string NewPassword { get; set; } = "";
    }
}
