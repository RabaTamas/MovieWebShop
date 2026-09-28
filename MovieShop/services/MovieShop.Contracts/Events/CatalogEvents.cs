namespace MovieShop.Contracts.Events;

/// <summary>
/// A Catalog Service publikálja, valahányszor egy film adata megváltozik
/// (létrehozás, módosítás, soft-delete, visszaállítás, videófeltöltés).
///
/// Az Order és az Auction Service ebből tartja karban a saját, csak olvasható
/// MovieSnapshot tábláját. Ez teszi lehetővé, hogy az ajánlórendszer kollaboratív
/// szűrése és a kosár árlekérdezése lokális join maradjon, hálózati hívás nélkül.
/// </summary>
public record MovieChanged
{
    public required int MovieId { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public required int Price { get; init; }
    public int? DiscountedPrice { get; init; }

    /// <summary>Kategórianevek — a fogyasztó service-eknek nincs saját Category táblájuk.</summary>
    public List<string> Categories { get; init; } = [];

    /// <summary>Soft-delete jelző. Törölt film nem ajánlható és nem tehető kosárba.</summary>
    public bool IsDeleted { get; init; }

    public string? VideoFileName { get; init; }

    /// <summary>
    /// Igaz, ha az esemény egy újonnan felvett filmről szól (nem módosításról). A User Service
    /// ebből küld „új film érkezett" push értesítést. Régi üzenetekben hiányzik → false.
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// A publikálás UTC ideje. A fogyasztók ez alapján dobják el a sorrendből
    /// kicsúszott, elavult üzeneteket (last-writer-wins).
    /// </summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
