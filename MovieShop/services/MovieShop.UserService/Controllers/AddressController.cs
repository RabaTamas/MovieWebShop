using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.ServiceDefaults;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AddressController : ControllerBase
{
    private readonly IAddressService _addressService;

    public AddressController(IAddressService addressService) => _addressService = addressService;

    [HttpGet]
    public async Task<ActionResult<List<AddressDto>>> GetUserAddresses()
        => Ok(await _addressService.GetUserAddressesAsync(User.GetUserId()));

    [HttpGet("{id}")]
    public async Task<ActionResult<AddressDto>> GetAddress(int id)
    {
        try
        {
            return Ok(await _addressService.GetAddressByIdAsync(id, User.GetUserId()));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<AddressDto>> CreateAddress(AddressDto addressDto)
    {
        var created = await _addressService.CreateAddressAsync(addressDto, User.GetUserId());
        return CreatedAtAction(nameof(GetAddress), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AddressDto>> UpdateAddress(int id, AddressDto addressDto)
    {
        try
        {
            return Ok(await _addressService.UpdateAddressAsync(id, addressDto, User.GetUserId()));
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
            return await _addressService.DeleteAddressAsync(id, User.GetUserId())
                ? NoContent()
                : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
