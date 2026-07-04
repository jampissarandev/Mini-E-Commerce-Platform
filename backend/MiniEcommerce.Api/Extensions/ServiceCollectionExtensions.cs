using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Repositories;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires up application-scoped services. The optional
    /// <paramref name="configuration"/> parameter lets the host pass its
    /// <see cref="IConfiguration"/> in so payment options bind from
    /// <c>Payments:Mock</c> without forcing this extension to build its own
    /// service provider.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Services
        services.AddScoped<IImageStorage, LocalImageStorage>();

        // Payment: bind failure-injection options and register the mock service.
        // In production, Mode defaults to AlwaysSucceed so checkout never fails
        // unexpectedly. Override via Payments:Mock:Mode in appsettings or via
        // the Payments__Mock__Mode env var for demos.
        if (configuration is not null)
        {
            services.Configure<MockPaymentOptions>(
                configuration.GetSection(MockPaymentOptions.SectionName));
        }
        services.AddScoped<IPaymentService, MockPaymentService>();

        return services;
    }
}
