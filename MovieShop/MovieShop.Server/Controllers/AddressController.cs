using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;
using System.Security.Claims;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AddressController : ControllerBase
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }

        [HttpGet]
        public async Task<ActionResult<List<AddressDto>>> GetUserAddresses()
        {
            var userId = GetCurrentUserId();
            var addresses = await _addressService.GetUserAddressesAsync(userId);
            return Ok(addresses);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AddressDto>> GetAddress(int id)
        {
            var userId = GetCurrentUserId();
            try
            {
                var address = await _addressService.GetAddressByIdAsync(id, userId);
                return Ok(address);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult<AddressDto>> CreateAddress(AddressDto addressDto)
        {
            var userId = GetCurrentUserId();
            var createdAddress = await _addressService.CreateAddressAsync(addressDto, userId);
            return CreatedAtAction(nameof(GetAddress), new { id = createdAddress.Id }, createdAddress);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AddressDto>> UpdateAddress(int id, AddressDto addressDto)
        {
            var userId = GetCurrentUserId();
            try
            {
                var updatedAddress = await _addressService.UpdateAddressAsync(id, addressDto, userId);
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
            var userId = GetCurrentUserId();
            try
            {
                var result = await _addressService.DeleteAddressAsync(id, userId);
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

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated or user ID is invalid");
            }
            return userId;
        }
    }
}