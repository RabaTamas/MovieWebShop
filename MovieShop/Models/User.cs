using Microsoft.AspNetCore.Mvc.ViewEngines;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace MovieShop.Models
{
    public class User
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



    }
}
