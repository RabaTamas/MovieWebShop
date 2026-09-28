using MassTransit;
using MovieShop.Contracts.Events;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Consumers;

/// <summary>
/// „Új film érkezett" push értesítés. A Catalog Service minden filmváltozásnál MovieChanged
/// eseményt publikál; ez a fogyasztó csak az IsNew jelzésűekre reagál (a monolitban ugyanezt
/// a filmfelvétel után indított Hangfire-feladat végzi).
///
/// A küldés hibái feliratkozásonként el vannak nyelve (a SendToAllAsync nem dob kivételt egyetlen
/// eszköz hibájára), így az újrapróbálás nem küld duplán; az értesítés Tag-je miatt egy esetleges
/// ismételt kézbesítés is csak lecseréli a már látható értesítést.
/// </summary>
public class MovieChangedConsumer : IConsumer<MovieChanged>
{
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<MovieChangedConsumer> _logger;

    public MovieChangedConsumer(IPushNotificationService pushService, ILogger<MovieChangedConsumer> logger)
    {
        _pushService = pushService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MovieChanged> context)
    {
        var movie = context.Message;
        if (!movie.IsNew || movie.IsDeleted)
            return;

        var result = await _pushService.SendToAllAsync(PushNotificationService.NewMovieMessage(
            movie.MovieId, movie.Title, movie.Description, movie.ImageUrl, movie.Price, movie.DiscountedPrice));

        _logger.LogInformation(
            "New movie push for {MovieId}: sent {Sent}, removed {Removed}, failed {Failed}",
            movie.MovieId, result.Sent, result.Removed, result.Failed);
    }
}
