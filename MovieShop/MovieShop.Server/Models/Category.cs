using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Movie>? Movies { get; set; }
    }
}
