using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IAuctionService
    {
        Task<List<AuctionDto>> GetActiveAuctionsAsync();
        Task<List<AuctionDto>> GetAllAuctionsAsync();
        Task<List<AuctionDto>> GetMyWonAuctionsAsync(int userId);
        Task<AuctionDto?> GetAuctionAsync(int id);
        Task<AuctionDto> CreateAuctionAsync(CreateAuctionRequest request);
        Task<BidResult> PlaceBidAsync(int auctionId, int userId, decimal amount);
        Task UpdateAuctionStatusesAsync();
        Task<AuctionDto?> EditAuctionAsync(int id, CreateAuctionRequest request);
        Task<bool> DeleteAuctionAsync(int id);
        Task<(string clientSecret, string publishableKey)?> CreatePaymentIntentAsync(int auctionId, int userId);
        Task<bool> ConfirmPaymentAsync(int auctionId, int userId, string paymentIntentId);
    }
}
