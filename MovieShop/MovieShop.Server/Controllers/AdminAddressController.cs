using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/admin/addresses")]
    [Authorize(Roles = "Admin")]
    public class AdminAddressController : ControllerBase
    {
        private readonly IAdminAddressService _adminAddressService;

        public AdminAddressController(IAdminAddressService adminAddressService)
        {
            _adminAddressService = adminAddressService;
        }

        [HttpGet]
        public async Task<ActionResult<List<AdminAddressDto>>> GetAllAddresses()
        {
            var addresses = await _adminAddressService.GetAllAddressesAsync();
            return Ok(addresses);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AdminAddressDto>> GetAddress(int id)
        {
            try
            {
                var address = await _adminAddressService.GetAddressByIdAsync(id);
                return Ok(address);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AddressDto>> UpdateAddress(int id, AddressDto addressDto)
        {
            try
            {
                var updatedAddress = await _adminAddressService.UpdateAddressAsync(id, addressDto);
                return Ok(updatedAddress);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAddress(int id)
        {
            try
            {
                var result = await _adminAddressService.DeleteAddressAsync(id);
                if (result)
                {
                    return NoContent();
                }
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}