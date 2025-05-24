
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieShop.Server.Models
{
    /*public class User
    {

        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public List<Address>? Addresses { get; set; }

        public List<Order>? Orders { get; set; }

        public ShoppingCart Cart { get; set; } = null!;
        public List<Review>? Reviews { get; set; }

        [Required]
        public string Role { get; set; } = "User";



    }*/


    public class User : IdentityUser<int>
    {
        public List<Address>? Addresses { get; set; }
        public List<Order>? Orders { get; set; }

        public ShoppingCart? Cart { get; set; }

        public List<Review>? Reviews { get; set; }
    }
}
