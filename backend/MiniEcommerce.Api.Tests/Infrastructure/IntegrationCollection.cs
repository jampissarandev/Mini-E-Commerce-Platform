namespace MiniEcommerce.Api.Tests.Infrastructure;

/// <summary>
/// xUnit collection that shares a single <see cref="ApiFactory"/> across all
/// integration tests in the same assembly. This avoids the per-class
/// WebApplicationFactory spin-up cost (≈ 1 s) and makes the suite ~10× faster.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Integration";
}
