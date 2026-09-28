using System.Security.Claims;

namespace MovieShop.ServiceDefaults;

/// <summary>
/// A monolitban minden controller saját GetCurrentUserId() metódust tartalmazott,
/// ugyanazzal a törzzsel. Mikroszervizeknél ez öt projektben duplikálódna, ezért
/// egyetlen közös kiterjesztésbe került.
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>A bejelentkezett felhasználó azonosítója, vagy 0, ha nincs érvényes claim.</summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst("sub")?.Value;

        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>A felhasználó megjelenítendő neve a tokenből, adatbázis-lekérdezés nélkül.</summary>
    public static string GetUserName(this ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Name)?.Value ?? "Ismeretlen";

    public static bool IsAdmin(this ClaimsPrincipal user)
        => user.IsInRole("Admin");
}
