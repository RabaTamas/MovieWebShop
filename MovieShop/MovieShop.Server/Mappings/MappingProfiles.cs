using AutoMapper;
using MovieShop.Server.Constants;
using MovieShop.Server.DTOs;
using MovieShop.Server.DTOs.TMDB;
using MovieShop.Server.Models;

namespace MovieShop.Server.Mappings
{
    public class MappingProfiles: Profile
    {
        public MappingProfiles()
        {
            // User mappings 
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.UserName))
                .ForMember(dest => dest.Role, opt => opt.Ignore());

            CreateMap<User, UserProfileDto>()
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email));



            // Movie mappings
            CreateMap<Movie, MovieListDto>();
            CreateMap<Movie, MovieDetailsDto>();
            CreateMap<Movie, MovieDetailsWithTmdbDto>();

            CreateMap<Movie, MovieAdminListDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.IsDeleted ? "Deleted" : "Active"));

            // Category mappings
            CreateMap<Category, CategoryDto>();

            // Review mappings
            CreateMap<Review, ReviewDto>()
               .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId));

            // Address mappings
            CreateMap<Address, AddressDto>();
            CreateMap<AddressDto, Address>();
            CreateMap<Address, AdminAddressDto>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User!.UserName))
                .ForMember(dest => dest.UserEmail, opt => opt.MapFrom(src => src.User!.Email))
                .ForMember(dest => dest.BillingOrdersCount, opt => opt.Ignore())
                .ForMember(dest => dest.ShippingOrdersCount, opt => opt.Ignore());

            // Order mappings
            CreateMap<Order, OrderDto>()
                .ForMember(dest => dest.Movies, opt => opt.MapFrom(src => src.OrderMovies))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.UserName))
                .ForMember(dest => dest.UserEmail, opt => opt.MapFrom(src => src.User.Email));

            CreateMap<OrderMovie, OrderMovieDto>()
                .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Movie.Title));

            // ShoppingCart mappings
            CreateMap<ShoppingCart, ShoppingCartDto>()
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.ShoppingCartMovies ?? new List<ShoppingCartMovie>()));

            CreateMap<ShoppingCartMovie, ShoppingCartMovieDto>()
                .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Movie.Title));
        }
    }
}
