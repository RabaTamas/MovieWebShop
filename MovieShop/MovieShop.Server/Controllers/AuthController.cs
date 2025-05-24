using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.Constants;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;

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
    }
}
