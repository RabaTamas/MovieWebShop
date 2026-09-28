using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.UserService.Data;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Models;

namespace MovieShop.UserService.Services;

public interface IAddressService
{
    Task<List<AddressDto>> GetUserAddressesAsync(int userId);
    Task<AddressDto> GetAddressByIdAsync(int addressId, int userId);
    Task<AddressDto> CreateAddressAsync(AddressDto addressDto, int userId);
    Task<AddressDto> UpdateAddressAsync(int addressId, AddressDto addressDto, int userId);
    Task<bool> DeleteAddressAsync(int addressId, int userId);
}

public class AddressService : IAddressService
{
    private readonly UsersDbContext _context;
    private readonly IMapper _mapper;
    private readonly IOrderServiceClient _orderClient;

    public AddressService(UsersDbContext context, IMapper mapper, IOrderServiceClient orderClient)
    {
        _context = context;
        _mapper = mapper;
        _orderClient = orderClient;
    }

    public async Task<List<AddressDto>> GetUserAddressesAsync(int userId)
    {
        var addresses = await _context.Addresses
            .Where(a => a.UserId == userId)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<AddressDto>>(addresses);
    }

    public async Task<AddressDto> GetAddressByIdAsync(int addressId, int userId)
    {
        var address = await _context.Addresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
            ?? throw new KeyNotFoundException($"Address with ID {addressId} not found for this user");

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
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
            ?? throw new KeyNotFoundException($"Address with ID {addressId} not found for this user");

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
            return false;

        // A monolitban ez egy lokális `_context.Orders.AnyAsync(...)` volt.
        // Mikroszervizekben a rendelések másik adatbázisban vannak, ezért
        // szinkron hívás megy az Order Service felé.
        if (await _orderClient.IsAddressUsedAsync(addressId))
            throw new InvalidOperationException("Cannot delete address as it is used in existing orders");

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync();
        return true;
    }
}
