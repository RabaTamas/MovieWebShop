using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IAdminAddressService
    {
        Task<List<AdminAddressDto>> GetAllAddressesAsync();
        Task<AdminAddressDto> GetAddressByIdAsync(int addressId);
        Task<AddressDto> UpdateAddressAsync(int addressId, AddressDto addressDto);
        Task<bool> DeleteAddressAsync(int addressId);
    }
}
