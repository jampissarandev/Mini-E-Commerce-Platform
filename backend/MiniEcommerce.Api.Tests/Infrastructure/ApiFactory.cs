using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Infrastructure;

/// <summary>
/// Test host for the API. Wires up:
///   - a known 64-byte JWT signing key so the production startup guard passes
///   - the EF Core in-memory provider with a unique database per test class
///   - a writable temp directory for image storage
///   - relaxed Identity email-confirmation so test login flows are simple
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// 64-character signing key. Long enough to satisfy the >= 32 byte guard in
    /// <c>Program.cs</c>. Never use this key in any non-test environment.
    /// </summary>
    public const string TestJwtKey =
        "test-signing-key-please-do-not-use-in-prod-32bytes-min";

    /// <summary>Unique DB name per test class so suites do not interfere.</summary>
    public string DatabaseName { get; } = $"TestDb_{Guid.NewGuid():N}";

    /// <summary>Temp directory for LocalImageStorage. Cleaned up on dispose.</summary>
    public string TempWebRoot { get; }

    public ApiFactory()
    {
        TempWebRoot = Path.Combine(Path.GetTempPath(), "MiniEcommerce.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempWebRoot);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // In-memory config layer — overrides appsettings.json values
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = "MiniEcommerce.Test",
                ["Jwt:Audience"] = "MiniEcommerce.Test.Audience",
                ["Jwt:ExpiresInMinutes"] = "60",
                // Point the connection string at a sentinel value; we replace the
                // DbContext registration below so it is never actually used.
                ["ConnectionStrings:Default"] = "InMemorySentinel",
                ["Cors:Origins:0"] = "http://localhost"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the production DbContext with the in-memory provider.
            // We must strip every EF Core / Npgsql descriptor from the service
            // collection first; otherwise both providers are registered and
            // DbContext initialization throws.
            var efDescriptors = services
                .Where(d => IsEntityFrameworkService(d.ServiceType, d.ImplementationType))
                .ToList();
            foreach (var d in efDescriptors)
                services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(DatabaseName));

            // Use a temp directory for IImageStorage so tests don't write to
            // the real wwwroot/images (which may not exist in CI).
            services.RemoveAll<IImageStorage>();
            services.AddScoped<IImageStorage>(_ =>
                new LocalImageStorage(
                    new TestWebHostEnvironment(TempWebRoot),
                    _.GetRequiredService<ILogger<LocalImageStorage>>()));

            // IPaymentService reads its mode from a single mutable holder so
            // tests can flip the failure-injection mode at runtime without
            // rebuilding the service provider. The holder is a singleton and
            // a live IOptions<T> wrapper is registered so both the service
            // and PaymentsController see the current value.
            var paymentHolder = new MockPaymentOptionsHolder();
            services.AddSingleton(paymentHolder);
            services.AddSingleton<IOptions<MockPaymentOptions>>(sp =>
                new LiveMockPaymentOptions(sp.GetRequiredService<MockPaymentOptionsHolder>()));
            services.AddScoped<IPaymentService>(sp =>
                new MockPaymentService(
                    sp.GetRequiredService<IOptions<MockPaymentOptions>>(),
                    sp.GetRequiredService<ILogger<MockPaymentService>>()));
        });
    }

    /// <summary>
    /// Flips the mock payment service into the requested failure-injection
    /// <paramref name="mode"/>. Visible to all subsequent scoped resolutions
    /// for the lifetime of the test host.
    /// </summary>
    public void SetPaymentMode(MockPaymentMode mode, decimal failIfAmountGreaterThan = 1000m)
    {
        var holder = Services.GetRequiredService<MockPaymentOptionsHolder>();
        holder.Set(new MockPaymentOptions
        {
            Mode = mode,
            FailIfAmountGreaterThan = failIfAmountGreaterThan,
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { Directory.Delete(TempWebRoot, recursive: true); } catch { /* best effort */ }
        }
        base.Dispose(disposing);
    }

    private static bool IsEntityFrameworkService(Type serviceType, Type? implementationType)
    {
        // Heuristic: any service whose type or implementation lives in the
        // EntityFrameworkCore or Npgsql assemblies is part of the EF provider
        // registration and must be removed before swapping providers.
        foreach (var t in new[] { serviceType, implementationType })
        {
            if (t is null) continue;
            var asm = t.Assembly.GetName().Name ?? string.Empty;
            if (asm.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                asm.StartsWith("Npgsql.EntityFrameworkCore", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Minimal IWebHostEnvironment stand-in for LocalImageStorage's constructor in tests.
/// LocalImageStorage only reads <see cref="IWebHostEnvironment.WebRootPath"/>.
/// </summary>
internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public TestWebHostEnvironment(string webRootPath)
    {
        WebRootPath = webRootPath;
        ContentRootPath = webRootPath;
        ContentRootFileProvider = new Microsoft.Extensions.FileProviders.NullFileProvider();
        WebRootFileProvider = new Microsoft.Extensions.FileProviders.NullFileProvider();
        EnvironmentName = "Testing";
        ApplicationName = "MiniEcommerce.Api.Tests";
    }
    public string WebRootPath { get; set; }
    public string ContentRootPath { get; set; }
    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; }
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
}
