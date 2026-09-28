namespace MovieShop.Contracts.Events;

/// <summary>
/// Az Auction Service publikálja, amikor egy filmre szóló aukció nyertese kifizette
/// a vételárat. A Catalog Service ugyanúgy jogosultságot ad rá, mint egy rendelésnél
/// — így a megnyert film azonnal streamelhető, anélkül hogy az Auction Service-nek
/// bármit tudnia kellene a streamingről.
/// </summary>
public record AuctionPaid
{
    public required int AuctionId { get; init; }
    public required int WinnerUserId { get; init; }

    /// <summary>Null, ha az aukció önálló gyűjtői tárgyra szólt, nem filmre.</summary>
    public int? MovieId { get; init; }

    public decimal Amount { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Aukció lezárult. Jelenleg naplózási és későbbi értesítési célt szolgál
/// (e-mail a nyertesnek); a licitelés valós idejű frissítése továbbra is
/// közvetlenül a SignalR hubon megy, nem üzenetsoron.
/// </summary>
public record AuctionEnded
{
    public required int AuctionId { get; init; }
    public int? WinnerUserId { get; init; }
    public int? MovieId { get; init; }
    public decimal FinalPrice { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
