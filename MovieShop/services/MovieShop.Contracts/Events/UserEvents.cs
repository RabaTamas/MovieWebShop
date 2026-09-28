namespace MovieShop.Contracts.Events;

/// <summary>
/// A User Service publikálja regisztrációkor, profilmódosításkor és törléskor.
///
/// Az Auction Service ebből tölti a licitálók megjelenítendő nevét, a Catalog
/// Service pedig az értékelések szerzőjének nevét — így egyik sem hívja a User
/// Service-t minden egyes listaelemnél (N+1 elkerülése).
/// </summary>
public record UserChanged
{
    public required int UserId { get; init; }
    public required string UserName { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
