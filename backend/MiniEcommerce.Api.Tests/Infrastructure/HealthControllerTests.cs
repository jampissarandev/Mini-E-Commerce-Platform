using System.Net;
using FluentAssertions;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Infrastructure;

/// <summary>
/// Smoke test: confirms the test host boots and the public /health endpoint
/// responds. Acts as a guard for the test harness itself.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class HealthControllerTests
{
    private readonly ApiFactory _factory;

    public HealthControllerTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
