using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Controllers;

[ApiController]
[Route("api/admin/addresses")]
[Authorize(Roles = "Admin")]
public class AdminAddressController : ControllerBase
{
    private readonly IAdminAddressService _adminAddressService;

    public AdminAddressController(IAdminAddressService adminAddressService)
        => _adminAddressService = adminAddressService;

    [HttpGet]
    public async Task<ActionResult<List<AdminAddressDto>>> GetAllAddresses()
        => Ok(await _adminAddressService.GetAllAddressesAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminAddressDto>> GetAddress(int id)
    {
        try
        {
            return Ok(await _adminAddressService.GetAddressByIdAsync(id));
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
            return Ok(await _adminAddressService.UpdateAddressAsync(id, addressDto));
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
            return await _adminAddressService.DeleteAddressAsync(id) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
