using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class HostConfigurationTests(QuerioApiFixture fixture)
{
    [Fact]
    public void Integration_tests_run_in_the_environment_the_factory_asks_for()
    {
        // Minimal hosting resolves IHostEnvironment while WebApplication.CreateBuilder runs,
        // so a late UseEnvironment call can be silently ignored. Pinning it here means the
        // environment-dependent behaviour under test is the behaviour we think it is.
        var environment = fixture.Factory.Services.GetRequiredService<IHostEnvironment>();

        environment.EnvironmentName.ShouldBe(Environments.Development);
    }

    [Fact]
    public void The_container_connection_string_actually_reached_the_application()
    {
        // Guards against the test suite quietly running against a developer's local database.
        fixture.ConnectionString.ShouldContain("Port=");
        fixture.ConnectionString.ShouldNotContain("5433");
    }
}
