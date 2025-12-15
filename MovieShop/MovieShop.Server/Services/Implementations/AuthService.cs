
using Microsoft.Extensions.Configuration;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MovieShop.Server.Constants;
using Microsoft.AspNetCore.Identity;
using MovieShop.Server.Services.Interfaces;
using Google.Apis.Auth;

namespace MovieShop.Server.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;

        public AuthService(
            UserManager<User> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            IMapper mapper,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
            _configuration = configuration;
        }

        public async Task<AuthResultDto> RegisterAsync(UserRegisterDto registerDto)
        {
            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                throw new ApplicationException("Email is already registered");
            }

            // Create new user
            var user = new User
            {
                UserName = registerDto.Name,
                Email = registerDto.Email,
                EmailConfirmed = true
            };

            // Create the user with password
            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ApplicationException($"Failed to create user: {errors}");
            }

            // Ensure the User role exists and assign it
            if (!await _roleManager.RoleExistsAsync(UserRoles.User))
            {
                await _roleManager.CreateAsync(new IdentityRole<int>(UserRoles.User));
            }
            await _userManager.AddToRoleAsync(user, UserRoles.User);

            // Get user roles
            var roles = await _userManager.GetRolesAsync(user);

            // Generate JWT token
            var token = GenerateJwtToken(user, roles);

            // Return authentication result
            return new AuthResultDto
            {
                Token = token,
                User = _mapper.Map<UserDto>(user),
                TokenExpiration = DateTime.UtcNow.AddDays(7)
            };
        }

        public async Task<AuthResultDto> LoginAsync(UserLoginDto loginDto)
        {
            // Find user by email
            var user = await _userManager.FindByEmailAsync(loginDto.Email);

            // Check if user exists and password is correct
            if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
            {
                throw new ApplicationException("Invalid email or password");
            }

            // Get user roles
            var roles = await _userManager.GetRolesAsync(user);

            // Generate JWT token
            var token = GenerateJwtToken(user, roles);

            // Create UserDto with role
            var userDto = _mapper.Map<UserDto>(user);
            userDto.Role = roles.FirstOrDefault() ?? "";

            // Return authentication result
            return new AuthResultDto
            {
                Token = token,
                User = userDto,
                TokenExpiration = DateTime.UtcNow.AddDays(7)
            };
        }

        public async Task<AuthResultDto> GoogleLoginAsync(string idToken)
        {
            try
            {
                // Verify the Google ID token
                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new[] { _configuration["Authentication:Google:ClientId"] }
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

                if (payload == null)
                {
                    throw new ApplicationException("Invalid Google token");
                }

                // Check if user exists
                var user = await _userManager.FindByEmailAsync(payload.Email);

                if (user == null)
                {
                    // Create new user from Google account
                    // Use email as username to avoid special character issues
                    var username = payload.Email.Split('@')[0]; // Use email prefix as username
                    
                    user = new User
                    {
                        UserName = username,
                        Email = payload.Email,
                        EmailConfirmed = true // Google accounts are already verified
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        throw new ApplicationException($"Failed to create user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                    }

                    // Assign default user role
                    await _userManager.AddToRoleAsync(user, UserRoles.User);
                }

                // Get user roles
                var roles = await _userManager.GetRolesAsync(user);

                // Generate JWT token
                var token = GenerateJwtToken(user, roles);

                var userDto = _mapper.Map<UserDto>(user);

                return new AuthResultDto
                {
                    Token = token,
                    User = userDto,
                    TokenExpiration = DateTime.UtcNow.AddDays(7)
                };
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Google login failed: {ex.Message}");
            }
        }

        public async Task<bool> IsAdminAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            return user != null && await _userManager.IsInRoleAsync(user, UserRoles.Admin);
        }

        public async Task<bool> AssignRoleAsync(int userId, string role)
        {
            // Ensure the role exists
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole<int>(role));
            }

            // Find the user
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return false;

            // Remove existing roles and add the new one
            var existingRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, existingRoles);
            await _userManager.AddToRoleAsync(user, role);

            return true;
        }

        public string GenerateJwtToken(User user, IList<string> roles)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured"));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
            };

            // Add roles as claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
