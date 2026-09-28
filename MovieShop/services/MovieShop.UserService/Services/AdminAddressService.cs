using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.UserService.Data;
using MovieShop.UserService.DTOs;

namespace MovieShop.UserService.Services;

public interface IAdminAddressService
{
    Task<List<AdminAddressDto>> GetAllAddressesAsync();
    Task<AdminAddressDto> GetAddressByIdAsync(int addressId);
    Task<AddressDto> UpdateAddressAsync(int addressId, AddressDto addressDto);
    Task<bool> DeleteAddressAsync(int addressId);
}

public class AdminAddressService : IAdminAddressService
{
    private readonly UsersDbContext _context;
    private readonly IMapper _mapper;
    private readonly IOrderServiceClient _orderClient;

    public AdminAddressService(UsersDbContext context, IMapper mapper, IOrderServiceClient orderClient)
    {
        _context = context;
        _mapper = mapper;
        _orderClient = orderClient;
    }

    /// <summary>
    /// A monolit verziója korrelált allekérdezéssel számolta a rendeléseket
    /// (`_context.Orders.Count(...)` a Select-en belül, címenként két lekérdezés).
    /// Itt a címek egy lekérdezéssel jönnek, a rendelésszámok pedig EGYETLEN
    /// batch-hívással az Order Service-től — N+1 helyett 1+1.
    /// </summary>
    public async Task<List<AdminAddressDto>> GetAllAddressesAsync()
    {
        var addresses = await _context.Addresses
            .Include(a => a.User)
            .OrderBy(a => a.Id)
            .AsNoTracking()
            .Select(a => new AdminAddressDto
            {
                Id = a.Id,
                Street = a.Street,
                City = a.City,
                Zip = a.Zip,
                UserId = a.UserId,
                UserName = a.User!.UserName ?? "",
                UserEmail = a.User.Email ?? ""
            })
            .ToListAsync();

        var usage = await _orderClient.GetAddressUsageAsync(addresses.Select(a => a.Id));

        foreach (var address in addresses)
        {
            if (usage.TryGetValue(address.Id, out var counts))
            {
                address.BillingOrdersCount = counts.BillingCount;
                address.ShippingOrdersCount = counts.ShippingCount;
            }
        }

        return addresses;
    }

    public async Task<AdminAddressDto> GetAddressByIdAsync(int addressId)
    {
        var address = await _context.Addresses
            .Include(a => a.User)
            .Where(a => a.Id == addressId)
            .AsNoTracking()
            .Select(a => new AdminAddressDto
            {
                Id = a.Id,
                Street = a.Street,
                City = a.City,
                Zip = a.Zip,
                UserId = a.UserId,
                UserName = a.User!.UserName ?? "",
                UserEmail = a.User.Email ?? ""
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Address with ID {addressId} not found");

        var usage = await _orderClient.GetAddressUsageAsync([addressId]);
        if (usage.TryGetValue(addressId, out var counts))
        {
            address.BillingOrdersCount = counts.BillingCount;
            address.ShippingOrdersCount = counts.ShippingCount;
        }

        return address;
    }

    public async Task<AddressDto> UpdateAddressAsync(int addressId, AddressDto addressDto)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == addressId)
            ?? throw new KeyNotFoundException($"Address with ID {addressId} not found");

        address.Street = addressDto.Street;
        address.City = addressDto.City;
        address.Zip = addressDto.Zip;

        await _context.SaveChangesAsync();

        return _mapper.Map<AddressDto>(address);
    }

    public async Task<bool> DeleteAddressAsync(int addressId)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == addressId);
        if (address == null)
            return false;

        if (await _orderClient.IsAddressUsedAsync(addressId))
            throw new InvalidOperationException("Cannot delete address as it is used in existing orders");

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync();
        return true;
    }
}
