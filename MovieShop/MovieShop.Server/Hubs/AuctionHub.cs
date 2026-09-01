using Microsoft.AspNetCore.SignalR;

namespace MovieShop.Server.Hubs
{
    public class AuctionHub : Hub
    {
        public async Task JoinAuction(int auctionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"auction_{auctionId}");
        }

        public async Task LeaveAuction(int auctionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction_{auctionId}");
        }
    }
}
