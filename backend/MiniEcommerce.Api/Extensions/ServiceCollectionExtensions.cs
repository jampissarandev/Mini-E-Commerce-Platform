using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Repositories;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Services
        services.AddScoped<IImageStorage, LocalImageStorage>();
        services.AddScoped<IPaymentService, MockPaymentService>();

        return services;
    }
}
