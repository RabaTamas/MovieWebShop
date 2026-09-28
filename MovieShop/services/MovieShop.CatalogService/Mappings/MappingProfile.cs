using AutoMapper;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Models;

namespace MovieShop.CatalogService.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Movie, MovieListDto>();
        CreateMap<Movie, MovieAdminListDto>();

        CreateMap<Movie, MovieDetailsDto>()
            .ForMember(dest => dest.Reviews, opt => opt.Ignore()); // külön töltjük, UserSnapshot joinnal

        CreateMap<Movie, MovieDetailsWithTmdbDto>()
            .ForMember(dest => dest.Reviews, opt => opt.Ignore())
            .ForMember(dest => dest.TmdbInfo, opt => opt.Ignore());

        CreateMap<Category, CategoryDto>();
        CreateMap<CategoryDto, Category>()
            .ForMember(dest => dest.Movies, opt => opt.Ignore());
    }
}
