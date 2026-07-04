using System.Runtime.CompilerServices;

namespace MiniEcommerce.Api.Tests.Infrastructure;

/// <summary>
/// Runs once when the test assembly is loaded. Sets the JWT signing key in the
/// environment so the production startup guard in <c>Program.cs</c> passes
/// before <see cref="ApiFactory"/>'s <c>ConfigureWebHost</c> overrides apply.
/// </summary>
internal static class AssemblyConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // 64-char key — long enough for the >= 32 byte HS256 guard.
        Environment.SetEnvironmentVariable(
            "Jwt__Key",
            "test-signing-key-please-do-not-use-in-prod-32bytes-min");
    }
}
