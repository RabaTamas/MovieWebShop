using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.Constants;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;
using System.Security.Claims;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResultDto>> Register(UserRegisterDto registerDto)
        {
            try
            {
                var result = await _authService.RegisterAsync(registerDto);
                return Ok(result);
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
                var result = await _authService.LoginAsync(loginDto);
                return Ok(result);
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
            var result = await _authService.AssignRoleAsync(userId, UserRoles.Admin);
            if (!result)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = "User is now an admin" });
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("remove-admin/{userId}")]
        public async Task<ActionResult> RemoveAdmin(int userId)
        {
            var result = await _authService.AssignRoleAsync(userId, UserRoles.User);
            if (!result)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = "Admin rights removed from user" });
        }

        [HttpPost("google-login")]
        public async Task<ActionResult<AuthResultDto>> GoogleLogin(GoogleLoginDto googleLoginDto)
        {
            try
            {
                var result = await _authService.GoogleLoginAsync(googleLoginDto.IdToken);
                return Ok(result);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/Auth/2fa/setup — generate TOTP secret + URI for QR code
        [Authorize]
        [HttpGet("2fa/setup")]
        public async Task<ActionResult<TwoFactorSetupDto>> GetTwoFactorSetup()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                var result = await _authService.GetTwoFactorSetupAsync(userId);
                return Ok(result);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/Auth/2fa/enable — verify code and activate 2FA, returns recovery codes
        [Authorize]
        [HttpPost("2fa/enable")]
        public async Task<ActionResult> EnableTwoFactor([FromBody] EnableTwoFactorDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                var recoveryCodes = await _authService.EnableTwoFactorAsync(userId, dto.Code);
                return Ok(new { recoveryCodes });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/Auth/2fa/disable — verify code and deactivate 2FA
        [Authorize]
        [HttpPost("2fa/disable")]
        public async Task<ActionResult> DisableTwoFactor([FromBody] EnableTwoFactorDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                await _authService.DisableTwoFactorAsync(userId, dto.Code);
                return Ok(new { message = "Two-factor authentication disabled" });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/Auth/2fa/status — check whether 2FA is enabled
        [Authorize]
        [HttpGet("2fa/status")]
        public async Task<ActionResult<TwoFactorStatusDto>> GetTwoFactorStatus()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isEnabled = await _authService.GetTwoFactorStatusAsync(userId);
            return Ok(new TwoFactorStatusDto { IsEnabled = isEnabled });
        }

        // POST /api/Auth/2fa/login — complete login with TOTP code after password step
        [HttpPost("2fa/login")]
        public async Task<ActionResult<AuthResultDto>> TwoFactorLogin([FromBody] TwoFactorLoginDto dto)
        {
            try
            {
                var result = await _authService.TwoFactorLoginAsync(dto.TwoFactorUserId, dto.Code);
                return Ok(result);
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
