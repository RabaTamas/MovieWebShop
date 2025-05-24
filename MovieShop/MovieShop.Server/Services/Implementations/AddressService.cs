using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Services
{
    public class AddressService : IAddressService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public AddressService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<List<AddressDto>> GetUserAddressesAsync(int userId)
        {
            var addresses = await _context.Addresses
                .Where(a => a.UserId == userId)
                .ToListAsync();

            return _mapper.Map<List<AddressDto>>(addresses);
        }

        public async Task<AddressDto> GetAddressByIdAsync(int addressId, int userId)
        {
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

            if (address == null)
            {
                throw new KeyNotFoundException($"Address with ID {addressId} not found for this user");
            }

            return _mapper.Map<AddressDto>(address);
        }

        public async Task<AddressDto> CreateAddressAsync(AddressDto addressDto, int userId)
        {
            var address = _mapper.Map<Address>(addressDto);
            address.UserId = userId;

            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();

            return _mapper.Map<AddressDto>(address);
        }

        public async Task<AddressDto> UpdateAddressAsync(int addressId, AddressDto addressDto, int userId)
        {
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

            if (address == null)
            {
                throw new KeyNotFoundException($"Address with ID {addressId} not found for this user");
            }

            // Update properties
            address.Street = addressDto.Street;
            address.City = addressDto.City;
            address.Zip = addressDto.Zip;

            await _context.SaveChangesAsync();

            return _mapper.Map<AddressDto>(address);
        }

        public async Task<bool> DeleteAddressAsync(int addressId, int userId)
        {
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

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