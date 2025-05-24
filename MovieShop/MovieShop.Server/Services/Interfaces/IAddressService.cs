using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IAddressService
    {
        Task<List<AddressDto>> GetUserAddressesAsync(int userId);
        Task<AddressDto> GetAddressByIdAsync(int addressId, int userId);
        Task<AddressDto> CreateAddressAsync(AddressDto addressDto, int userId);
        Task<AddressDto> UpdateAddressAsync(int addressId, AddressDto addressDto, int userId);
        Task<bool> DeleteAddressAsync(int addressId, int userId);
    }
}
