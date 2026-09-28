namespace MovieShop.Contracts.Events;

/// <summary>
/// Az Order Service publikálja, amikor egy rendelés fizetése sikeresen lezárult.
///
/// Ez a rendszer legfontosabb eseménye: a Catalog Service ebből építi fel az
/// Entitlements projekcióját (ki melyik filmet nézheti). Enélkül a streaming
/// minden egyes HLS playlist-kérésnél szinkron hívást indítana az Order
/// Service felé — lejátszás közben, minden minőségváltásnál.
/// </summary>
public record OrderCompleted
{
    public required int OrderId { get; init; }
    public required int UserId { get; init; }

    /// <summary>A rendelésben szereplő filmek azonosítói — mindegyikre jogosultság keletkezik.</summary>
    public required List<int> MovieIds { get; init; }

    public int TotalPrice { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Egy rendelés kikerült a teljesített (Completed) állapotból. A Catalog visszavonja
/// a felsorolt filmek jogosultságát, így a felhasználó már nem tudja streamelni őket.
/// </summary>
public record OrderRevoked
{
    public required int OrderId { get; init; }
    public required int UserId { get; init; }

    /// <summary>
    /// Csak azok a filmek, amelyeket a felhasználó ezután egyetlen teljesített
    /// rendelésből sem birtokol — egy másik rendelésből megvett film hozzáférése megmarad.
    /// </summary>
    public required List<int> MovieIds { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
