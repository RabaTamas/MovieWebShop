using Microsoft.AspNetCore.Identity;

namespace MovieShop.UserService.Models;

/// <summary>
/// A monolit User modelljéhez képest itt már NINCSENEK Orders, Cart és Reviews
/// navigációs property-k. Azok az adatok más service-ek adatbázisában élnek, és
/// a User Service-nek nincs — és szándékosan nem is lehet — rálátása rájuk.
///
/// Ez a szétbontás lényege: a rendeléslista lekérdezéséhez az Order Service-t kell
/// hívni, nem egy Include()-ot írni.
/// </summary>
public class User : IdentityUser<int>
{
    public List<Address> Addresses { get; set; } = [];
}
