using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Services.Implementations
{
    public class ShoppingCartService : IShoppingCartService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public ShoppingCartService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<ShoppingCartDto?> GetCartByUserIdAsync(int userId)
        {
            var cart = await _context.ShoppingCarts
                .Include(c => c.ShoppingCartMovies)
                    .ThenInclude(i => i.Movie)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            // If cart doesn't exist, create a new one
            if (cart == null)
            {
                cart = new ShoppingCart { UserId = userId };
                await _context.ShoppingCarts.AddAsync(cart);
                await _context.SaveChangesAsync();
            }

            return _mapper.Map<ShoppingCartDto>(cart);
        }

        public async Task<bool> AddToCartAsync(int userId, int movieId, int quantity)
        {
            try
            {
                // Get or create cart
                var cart = await _context.ShoppingCarts
                    .Include(c => c.ShoppingCartMovies)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new ShoppingCart { UserId = userId };
                    await _context.ShoppingCarts.AddAsync(cart);
                    await _context.SaveChangesAsync();

                    
                    cart = await _context.ShoppingCarts
                        .Include(c => c.ShoppingCartMovies)
                        .FirstOrDefaultAsync(c => c.UserId == userId);
                }

                // Check if movie exists
                var movie = await _context.Movies.FindAsync(movieId);
                if (movie == null)
                    return false;

                // Check if item already exists in cart
                var existingItem = cart.ShoppingCartMovies.FirstOrDefault(i => i.MovieId == movieId);
                if (existingItem != null)
                {
                    // Update quantity
                    existingItem.Quantity += quantity;
                }
                else
                {
                    // Add new item
                    cart.ShoppingCartMovies.Add(new ShoppingCartMovie
                    {
                        MovieId = movieId,
                        Quantity = quantity,
                        PriceAtOrder = movie.DiscountedPrice ?? movie.Price,
                        ShoppingCartId = cart.Id
                    });
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateCartItemQuantityAsync(int userId, int movieId, int quantity)
        {
            try
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.ShoppingCartMovies)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                    return false;

                var item = cart.ShoppingCartMovies.FirstOrDefault(i => i.MovieId == movieId);
                if (item == null)
                    return false;

                if (quantity > 0)
                {
                    item.Quantity = quantity;
                }
                else
                {
                    
                    cart.ShoppingCartMovies.Remove(item);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveFromCartAsync(int userId, int movieId)
        {
            try
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.ShoppingCartMovies)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                    return false;

                var item = cart.ShoppingCartMovies.FirstOrDefault(i => i.MovieId == movieId);
                if (item == null)
                    return false;

                cart.ShoppingCartMovies.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ClearCartAsync(int userId)
        {
            try
            {
                var cart = await _context.ShoppingCarts
                    .Include(c => c.ShoppingCartMovies)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                    return false;

                cart.ShoppingCartMovies.Clear();
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
