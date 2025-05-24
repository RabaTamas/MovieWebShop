using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.DTOs
{
    public class UpdateEmailDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string NewEmail { get; set; } = "";
    }
}
