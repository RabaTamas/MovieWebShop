using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.ServiceDefaults;
using MovieShop.UserService.Constants;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResultDto>> Register(UserRegisterDto registerDto)
    {
        try
        {
            return Ok(await _authService.RegisterAsync(registerDto));
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResultDto>> Login(UserLoginDto loginDto)
    {
        try
        {
            return Ok(await _authService.LoginAsync(loginDto));
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("google-login")]
    public async Task<ActionResult<AuthResultDto>> GoogleLogin(GoogleLoginDto googleLoginDto)
    {
        try
        {
            return Ok(await _authService.GoogleLoginAsync(googleLoginDto.IdToken));
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("make-admin/{userId}")]
    public async Task<ActionResult> MakeAdmin(int userId)
    {
        if (!await _authService.AssignRoleAsync(userId, UserRoles.Admin))
            return NotFound(new { message = "User not found" });

        return Ok(new { message = "User is now an admin" });
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("remove-admin/{userId}")]
    public async Task<ActionResult> RemoveAdmin(int userId)
    {
        if (!await _authService.AssignRoleAsync(userId, UserRoles.User))
            return NotFound(new { message = "User not found" });

        return Ok(new { message = "Admin rights removed from user" });
    }

    // ── Kétfaktoros hitelesítés ───────────────────────────────────────────────

    [Authorize]
    [HttpGet("2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupDto>> GetTwoFactorSetup()
    {
        try
        {
            return Ok(await _authService.GetTwoFactorSetupAsync(User.GetUserId()));
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<ActionResult> EnableTwoFactor([FromBody] EnableTwoFactorDto dto)
    {
        try
        {
            var recoveryCodes = await _authService.EnableTwoFactorAsync(User.GetUserId(), dto.Code);
            return Ok(new { recoveryCodes });
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<ActionResult> DisableTwoFactor([FromBody] EnableTwoFactorDto dto)
    {
        try
        {
            await _authService.DisableTwoFactorAsync(User.GetUserId(), dto.Code);
            return Ok(new { message = "Two-factor authentication disabled" });
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("2fa/status")]
    public async Task<ActionResult<TwoFactorStatusDto>> GetTwoFactorStatus()
    {
        var isEnabled = await _authService.GetTwoFactorStatusAsync(User.GetUserId());
        return Ok(new TwoFactorStatusDto { IsEnabled = isEnabled });
    }

    [HttpPost("2fa/login")]
    public async Task<ActionResult<AuthResultDto>> TwoFactorLogin([FromBody] TwoFactorLoginDto dto)
    {
        try
        {
            return Ok(await _authService.TwoFactorLoginAsync(dto.TwoFactorUserId, dto.Code));
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
