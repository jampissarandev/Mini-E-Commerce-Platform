using FluentAssertions;
using MiniEcommerce.Api.Services;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for the refresh-cookie Secure-flag policy (ADR 0005).
/// Centralising the policy in <see cref="RefreshCookieOptions.ShouldUseSecure"/>
/// lets us assert the per-environment rule without spinning up the full
/// integration pipeline.
/// </summary>
public class RefreshCookieOptionsTests
{
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    [InlineData("Staging", false)]
    [InlineData("Testing", false)]
    public void ShouldUseSecure_IsTrueOnlyInProduction(string envName, bool expected)
    {
        var env = new TestWebHostEnvironment("/tmp") { EnvironmentName = envName };

        var actual = RefreshCookieOptions.ShouldUseSecure(env);

        actual.Should().Be(expected);
    }
}
