using Microsoft.AspNetCore.SignalR;

namespace MovieShop.AuctionService.Hubs;

/// <summary>
/// Az aukció valós idejű csatornája. Változatlanul átvehető a monolitból,
/// mert nem tartalmaz üzleti logikát — csak csoportkezelést.
///
/// FONTOS SKÁLÁZÁSI KORLÁT: a SignalR csoportok alapértelmezetten a
/// szerverpéldány memóriájában élnek. Ha az Auction Service-t több példányra
/// skálázod, a gateway ragadós útválasztása (sticky session) nem elég — egy
/// Redis backplane kell (`AddStackExchangeRedis`), különben a licit csak
/// annak a példánynak a klienseihez jut el, ahol a HTTP-kérés befutott.
/// Ez a monolitban is fennálló korlát volt, csak ott egy példány futott.
/// </summary>
public class AuctionHub : Hub
{
    public Task JoinAuction(int auctionId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"auction_{auctionId}");

    public Task LeaveAuction(int auctionId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction_{auctionId}");
}
