using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Data;
using MovieShop.Models;

namespace MovieShop.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class MoviesController : BaseController<Movie>
    {
        public MoviesController(AppDbContext context) : base(context) { }
    }
}
