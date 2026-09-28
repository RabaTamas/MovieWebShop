using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.ServiceDefaults;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IProfileService _profileService;

    public UserController(IProfileService profileService) => _profileService = profileService;

    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized("User ID not found in token");

        var profile = await _profileService.GetProfileAsync(userId);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailDto dto)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized("User ID not found in token");

        return await _profileService.UpdateEmailAsync(userId, dto)
            ? NoContent()
            : BadRequest("Email update failed");
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized("User ID not found in token");

        return await _profileService.ChangePasswordAsync(userId, dto)
            ? NoContent()
            : BadRequest("Password modification failed.");
    }

    /// <summary>
    /// Megjelenítési név módosítása — a Chat Service `update_display_name`
    /// eszköze hívja a felhasználó tokenjével.
    /// </summary>
    [HttpPut("display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameDto dto)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized("User ID not found in token");

        if (string.IsNullOrWhiteSpace(dto.NewName))
            return BadRequest(new { message = "Name cannot be empty." });

        var (success, error) = await _profileService.UpdateDisplayNameAsync(userId, dto.NewName);

        if (success)
            return Ok(new { message = $"Your display name has been updated to '{dto.NewName}'." });

        return error == "User not found"
            ? NotFound(new { message = error })
            : BadRequest(new { message = error });
    }

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
        => Ok(await _profileService.GetAllUsersAsync());

    [HttpDelete("{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        if (User.GetUserId() == userId)
            return BadRequest("You cannot delete your own account");

        return await _profileService.DeleteUserAsync(userId)
            ? NoContent()
            : NotFound("User not found");
    }
}
