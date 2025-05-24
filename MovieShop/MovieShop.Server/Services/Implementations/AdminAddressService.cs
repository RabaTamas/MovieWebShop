using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Services
{
    public class AdminAddressService : IAdminAddressService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public AdminAddressService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<List<AdminAddressDto>> GetAllAddressesAsync()
        {
            var addresses = await _context.Addresses
                .Include(a => a.User)
                .Select(a => new AdminAddressDto
                {
                    Id = a.Id,
                    Street = a.Street,
                    City = a.City,
                    Zip = a.Zip,
                    UserId = a.UserId,
                    UserName = a.User!.UserName ?? "",
                    UserEmail = a.User.Email ?? "",
                    BillingOrdersCount = _context.Orders.Count(o => o.BillingAddressId == a.Id),
                    ShippingOrdersCount = _context.Orders.Count(o => o.ShippingAddressId == a.Id)
                })
                .OrderBy(a => a.Id)
                .ToListAsync();

            return addresses;
        }

        public async Task<AdminAddressDto> GetAddressByIdAsync(int addressId)
        {
            var address = await _context.Addresses
                .Include(a => a.User)
                .Where(a => a.Id == addressId)
                .Select(a => new AdminAddressDto
                {
                    Id = a.Id,
                    Street = a.Street,
                    City = a.City,
                    Zip = a.Zip,
                    UserId = a.UserId,
                    UserName = a.User!.UserName ?? "",
                    UserEmail = a.User.Email ?? "",
                    BillingOrdersCount = _context.Orders.Count(o => o.BillingAddressId == a.Id),
                    ShippingOrdersCount = _context.Orders.Count(o => o.ShippingAddressId == a.Id)
                })
                .FirstOrDefaultAsync();

            if (address == null)
            {
                throw new KeyNotFoundException($"Address with ID {addressId} not found");
            }

            return address;
        }

        public async Task<AddressDto> UpdateAddressAsync(int addressId, AddressDto addressDto)
        {
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId);

            if (address == null)
            {
                throw new KeyNotFoundException($"Address with ID {addressId} not found");
            }

            // Update properties
            address.Street = addressDto.Street;
            address.City = addressDto.City;
            address.Zip = addressDto.Zip;

            await _context.SaveChangesAsync();

            return _mapper.Map<AddressDto>(address);
        }

        public async Task<bool> DeleteAddressAsync(int addressId)
        {
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId);

            if (address == null)
            {
                return false;
            }

            // Check if address is being used in any orders
            bool isUsedInOrders = await _context.Orders
                .AnyAsync(o => o.BillingAddressId == addressId || o.ShippingAddressId == addressId);

            if (isUsedInOrders)
            {
                throw new InvalidOperationException("Cannot delete address as it is used in existing orders");
            }

            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}