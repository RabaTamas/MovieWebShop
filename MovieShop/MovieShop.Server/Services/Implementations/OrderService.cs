using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Controllers;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly IShoppingCartService _cartService;

        public OrderService(
            AppDbContext context,
            IMapper mapper,
            IShoppingCartService cartService)
        {
            _context = context;
            _mapper = mapper;
            _cartService = cartService;
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersByUserIdAsync(int userId)
        {
            var orders = await _context.Orders
                .Include(o => o.BillingAddress)
                .Include(o => o.ShippingAddress)
                .Include(o => o.OrderMovies)
                    .ThenInclude(om => om.Movie)
                .Where(o => o.UserId == userId)
                .AsNoTracking()
                .ToListAsync();

            return _mapper.Map<IEnumerable<OrderDto>>(orders);
        }

        public async Task<OrderDto?> GetOrderByIdAsync(int orderId, int userId)
        {
            var order = await _context.Orders
                .Include(o => o.BillingAddress)
                .Include(o => o.ShippingAddress)
                .Include(o => o.OrderMovies)
                    .ThenInclude(om => om.Movie)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            return order == null ? null : _mapper.Map<OrderDto>(order);
        }

        public async Task<OrderDto?> CreateOrderFromCartAsync(int userId, OrderRequestDto dto)
        {
            // Fetch cart
            var cartDto = await _cartService.GetCartByUserIdAsync(userId);
            if (cartDto == null || !cartDto.Items.Any())
                return null;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create billing address
                var billingAddress = _mapper.Map<Address>(dto.BillingAddress);
                billingAddress.UserId = userId;
                
                await _context.Addresses.AddAsync(billingAddress);
                await _context.SaveChangesAsync();

                // Calculate total price (no quantity, each movie is 1 license)
                int totalPrice = cartDto.Items.Sum(i => i.PriceAtOrder);

                // Create order
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = totalPrice,
                    BillingAddressId = billingAddress.Id,
                    ShippingAddressId = null // No shipping for digital products
                };

                await _context.Orders.AddAsync(order);
                await _context.SaveChangesAsync();

                // Add order items (quantity always 1 for digital products)
                foreach (var item in cartDto.Items)
                {
                    var orderMovie = new OrderMovie
                    {
                        OrderId = order.Id,
                        MovieId = item.MovieId,
                        Quantity = 1, // Always 1 for digital license
                        PriceAtOrder = item.PriceAtOrder
                    };

                    await _context.OrderMovies.AddAsync(orderMovie);
                }
                await _context.SaveChangesAsync();

                // Clear cart
                await _cartService.ClearCartAsync(userId);

                // Load related data for the response
                order = await _context.Orders
                    .Include(o => o.BillingAddress)
                    .Include(o => o.ShippingAddress)
                    .Include(o => o.OrderMovies)
                        .ThenInclude(om => om.Movie)
                    .FirstOrDefaultAsync(o => o.Id == order.Id);

                await transaction.CommitAsync();

                return _mapper.Map<OrderDto>(order);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error creating order: {ex.Message}");
                await transaction.RollbackAsync();
                return null;
            }
        }

        public async Task<bool> CancelOrderAsync(int orderId, int userId)
        {
            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                    return false;

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }



        public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.BillingAddress)
                .Include(o => o.ShippingAddress)
                .Include(o => o.OrderMovies)
                    .ThenInclude(om => om.Movie)
                .AsNoTracking()
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return _mapper.Map<IEnumerable<OrderDto>>(orders);
        }

        public async Task<OrderDto?> GetOrderByIdAdminAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.BillingAddress)
                .Include(o => o.ShippingAddress)
                .Include(o => o.OrderMovies)
                    .ThenInclude(om => om.Movie)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            return order == null ? null : _mapper.Map<OrderDto>(order);
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
        {
            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                    return false;

                // Validate status is a valid enum value
                if (!Enum.TryParse<OrderStatus>(status, out _))
                    return false;

                order.Status = status;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersByStatusAsync(string status)
        {
            // Validate status is a valid enum value
            if (!Enum.TryParse<OrderStatus>(status, out _))
                return Enumerable.Empty<OrderDto>();

            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.BillingAddress)
                .Include(o => o.ShippingAddress)
                .Include(o => o.OrderMovies)
                    .ThenInclude(om => om.Movie)
                .Where(o => o.Status == status)
                .AsNoTracking()
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return _mapper.Map<IEnumerable<OrderDto>>(orders);
        }

        public async Task<OrderStatisticsDto> GetOrderStatisticsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var stats = new OrderStatisticsDto
            {
                TotalOrders = await _context.Orders.CountAsync(),
                TotalRevenue = await _context.Orders.SumAsync(o => o.TotalPrice),
                OrdersToday = await _context.Orders.CountAsync(o => o.OrderDate >= today && o.OrderDate < tomorrow),
                RevenueToday = await _context.Orders.Where(o => o.OrderDate >= today && o.OrderDate < tomorrow).SumAsync(o => o.TotalPrice)
            };

            // Get orders by status
            var ordersByStatus = await _context.Orders
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var statusGroup in ordersByStatus)
            {
                stats.OrdersByStatus[statusGroup.Status] = statusGroup.Count;
            }

            // Get top selling movies
            var topSellingMovies = await _context.OrderMovies
                .Include(om => om.Movie)
                .GroupBy(om => new { om.MovieId, om.Movie.Title })
                .Select(g => new TopSellingMovieDto
                {
                    MovieId = g.Key.MovieId,
                    Title = g.Key.Title,
                    Quantity = g.Sum(om => om.Quantity),
                    Revenue = g.Sum(om => om.Quantity * om.PriceAtOrder)
                })
                .OrderByDescending(m => m.Revenue)
                .Take(5)
                .ToListAsync();

            stats.TopSellingMovies = topSellingMovies;

            return stats;
        }

        public async Task<bool> HasUserPurchasedMovieAsync(int userId, int movieId)
        {
            var completedStatus = OrderStatus.Completed.ToString();
            
            return await _context.Orders
                .Where(o => o.UserId == userId && o.Status == completedStatus)
                .AnyAsync(o => o.OrderMovies.Any(om => om.MovieId == movieId));
        }

        public async Task<IEnumerable<int>> GetPurchasedMovieIdsAsync(int userId)
        {
            var completedStatus = OrderStatus.Completed.ToString();
            
            var movieIds = await _context.Orders
                .Where(o => o.UserId == userId && o.Status == completedStatus)
                .SelectMany(o => o.OrderMovies.Select(om => om.MovieId))
                .Distinct()
                .ToListAsync();

            return movieIds;
        }
    }
}
