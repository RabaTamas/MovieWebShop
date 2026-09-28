using Microsoft.EntityFrameworkCore;
using MovieShop.OrderService.Data;
using MovieShop.OrderService.DTOs;
using MovieShop.OrderService.Models;

namespace MovieShop.OrderService.Services;

public interface IShoppingCartService
{
    Task<ShoppingCartDto> GetCartByUserIdAsync(int userId);
    Task<bool> AddToCartAsync(int userId, int movieId, int quantity);
    Task<bool> UpdateCartItemQuantityAsync(int userId, int movieId, int quantity);
    Task<bool> RemoveFromCartAsync(int userId, int movieId);
    Task<bool> ClearCartAsync(int userId);
}

public class ShoppingCartService : IShoppingCartService
{
    private readonly OrdersDbContext _context;
    private readonly ILogger<ShoppingCartService> _logger;

    public ShoppingCartService(OrdersDbContext context, ILogger<ShoppingCartService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ShoppingCartDto> GetCartByUserIdAsync(int userId)
    {
        var cart = await GetOrCreateCartAsync(userId);

        // A filmcímek a lokális MovieSnapshot táblából jönnek — a monolitban ez
        // `.ThenInclude(i => i.Movie)` volt, itt is egyetlen lekérdezés marad.
        var items = await _context.ShoppingCartMovies
            .Where(scm => scm.ShoppingCartId == cart.Id)
            .AsNoTracking()
            .Select(scm => new ShoppingCartMovieDto
            {
                MovieId = scm.MovieId,
                Quantity = scm.Quantity,
                PriceAtOrder = scm.PriceAtOrder,
                Title = _context.MovieSnapshots
                    .Where(m => m.MovieId == scm.MovieId)
                    .Select(m => m.Title)
                    .FirstOrDefault() ?? "Unknown movie"
            })
            .ToListAsync();

        return new ShoppingCartDto { Id = cart.Id, UserId = userId, Items = items };
    }

    public async Task<bool> AddToCartAsync(int userId, int movieId, int quantity)
    {
        try
        {
            var cart = await GetOrCreateCartAsync(userId);

            // A film létezését és árát a snapshotból nézzük. Ha a snapshot még nem
            // érkezett meg (épp most hozták létre a filmet), a hozzáadás elutasul —
            // ez a végleges konzisztencia ára, másodperces nagyságrendű ablakkal.
            var movie = await _context.MovieSnapshots
                .AsNoTracking()
                // A monolit `_context.Movies.FindAsync(movieId)`-val kereste a filmet,
                // ami a soft-delete-elt filmeket is megtalálja — itt is így marad.
                .FirstOrDefaultAsync(m => m.MovieId == movieId);

            if (movie == null)
            {
                _logger.LogWarning("A(z) {MovieId} film nem található a snapshotban", movieId);
                return false;
            }

            var existingItem = await _context.ShoppingCartMovies
                .FirstOrDefaultAsync(scm => scm.ShoppingCartId == cart.Id && scm.MovieId == movieId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                _context.ShoppingCartMovies.Add(new ShoppingCartMovie
                {
                    ShoppingCartId = cart.Id,
                    MovieId = movieId,
                    Quantity = quantity,
                    PriceAtOrder = movie.DiscountedPrice ?? movie.Price
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a kosárba helyezéskor — film: {MovieId}", movieId);
            return false;
        }
    }

    public async Task<bool> UpdateCartItemQuantityAsync(int userId, int movieId, int quantity)
    {
        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart == null)
            return false;

        var item = await _context.ShoppingCartMovies
            .FirstOrDefaultAsync(scm => scm.ShoppingCartId == cart.Id && scm.MovieId == movieId);

        if (item == null)
            return false;

        if (quantity > 0)
            item.Quantity = quantity;
        else
            _context.ShoppingCartMovies.Remove(item);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFromCartAsync(int userId, int movieId)
    {
        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart == null)
            return false;

        var item = await _context.ShoppingCartMovies
            .FirstOrDefaultAsync(scm => scm.ShoppingCartId == cart.Id && scm.MovieId == movieId);

        if (item == null)
            return false;

        _context.ShoppingCartMovies.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ClearCartAsync(int userId)
    {
        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart == null)
            return false;

        var items = await _context.ShoppingCartMovies
            .Where(scm => scm.ShoppingCartId == cart.Id)
            .ToListAsync();

        _context.ShoppingCartMovies.RemoveRange(items);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// A kosár első használatkor jön létre. A monolitban ezt részben a seedelő
    /// végezte (az adminnak előre létrehozott kosarat); itt egységesen lusta
    /// létrehozás van, mert a User Service nem tud a kosarak létezéséről.
    /// </summary>
    private async Task<ShoppingCart> GetOrCreateCartAsync(int userId)
    {
        var cart = await _context.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart != null)
            return cart;

        cart = new ShoppingCart { UserId = userId };
        _context.ShoppingCarts.Add(cart);
        await _context.SaveChangesAsync();

        return cart;
    }
}
