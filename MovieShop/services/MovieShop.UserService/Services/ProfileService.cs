using MassTransit;
using Microsoft.AspNetCore.Identity;
using MovieShop.Contracts.Events;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Models;

namespace MovieShop.UserService.Services;

public interface IProfileService
{
    Task<UserProfileDto?> GetProfileAsync(int userId);
    Task<bool> UpdateEmailAsync(int userId, UpdateEmailDto dto);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    Task<List<UserDto>> GetAllUsersAsync();
    Task<bool> DeleteUserAsync(int userId);
    Task<(bool Success, string? Error)> UpdateDisplayNameAsync(int userId, string newName);
}

/// <summary>
/// A monolit UserService-ének megfelelője. (Az osztály neve azért ProfileService,
/// hogy ne ütközzön a MovieShop.UserService névtérrel.)
/// </summary>
public class ProfileService : IProfileService
{
    private readonly UserManager<User> _userManager;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IAuctionServiceClient _auctionClient;

    public ProfileService(
        UserManager<User> userManager,
        IPublishEndpoint publishEndpoint,
        IAuctionServiceClient auctionClient)
    {
        _userManager = userManager;
        _publishEndpoint = publishEndpoint;
        _auctionClient = auctionClient;
    }

    public async Task<UserProfileDto?> GetProfileAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return null;

        return new UserProfileDto { Email = user.Email ?? string.Empty };
    }

    public async Task<bool> UpdateEmailAsync(int userId, UpdateEmailDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        var existingUser = await _userManager.FindByEmailAsync(dto.NewEmail);
        if (existingUser != null && existingUser.Id != userId)
            return false;

        user.Email = dto.NewEmail;
        user.UserName = dto.NewEmail;
        user.NormalizedEmail = dto.NewEmail.ToUpper();
        user.NormalizedUserName = dto.NewEmail.ToUpper();

        var result = await _userManager.UpdateAsync(user);

        // A névváltozásról értesíteni kell a snapshotot tartó service-eket
        if (result.Succeeded)
            await PublishUserChangedAsync(user, isDeleted: false);

        return result.Succeeded;
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        return result.Succeeded;
    }

    /// <summary>
    /// A monolit a szerepkört `IsInRoleAsync(...).Result`-tal kérdezte le egy Select-en
    /// belül (sync-over-async). Az eredmény azonos, de itt aszinkron módon töltődik be.
    /// </summary>
    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = _userManager.Users.ToList();
        var result = new List<UserDto>(users.Count);

        foreach (var user in users)
        {
            result.Add(new UserDto
            {
                Id = user.Id,
                Name = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = await _userManager.IsInRoleAsync(user, "Admin") ? "Admin" : "User"
            });
        }

        return result;
    }

    /// <summary>
    /// Megjelenítési név módosítása. A monolit AgentService ezt közvetlenül a
    /// UserManageren végezte; mikroszervizekben a Chat Service nem éri el az
    /// Identity táblákat, ezért kapott saját végpontot.
    /// </summary>
    public async Task<(bool Success, string? Error)> UpdateDisplayNameAsync(int userId, string newName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return (false, "User not found");

        user.UserName = newName;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        await PublishUserChangedAsync(user, isDeleted: false);
        return (true, null);
    }

    /// <summary>
    /// Felhasználó törlése a monolit kaszkádszabályaival egyezően.
    ///
    /// A monolitban a Bid → User kapcsolat Restrict volt: licitet leadott felhasználó
    /// törlésekor az adatbázis elutasította a műveletet, és a kérés hibával zárult.
    /// A licitek itt az Auction Service-ben vannak, ezért a törlés előtt rákérdezünk.
    ///
    /// A többi függő adat (rendelések, kosár, értékelések, lejátszási pozíciók) a
    /// monolitban kaszkádolva törlődött. Itt a UserChanged(IsDeleted) esemény
    /// alapján minden service a saját adatbázisából törli őket.
    /// </summary>
    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        if (await _auctionClient.HasBidsAsync(userId))
            throw new InvalidOperationException("The user has placed bids and cannot be deleted.");

        var result = await _userManager.DeleteAsync(user);

        if (result.Succeeded)
            await PublishUserChangedAsync(user, isDeleted: true);

        return result.Succeeded;
    }

    private Task PublishUserChangedAsync(User user, bool isDeleted)
        => _publishEndpoint.Publish(new UserChanged
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            IsDeleted = isDeleted
        });
}
