using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class HealthEndpointsTests(QuerioApiFixture fixture)
{
    [Fact]
    public async Task Liveness_probe_reports_healthy()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_probe_reports_the_database_check()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var report = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        report.GetProperty("status").GetString().ShouldBe("Healthy");

        // Readiness is only meaningful if it actually exercises the dependencies.
        var checkNames = report.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .ToArray();

        checkNames.ShouldContain("database");
    }

    [Fact]
    public async Task Unmapped_route_returns_problem_details_rather_than_an_empty_body()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("status").GetInt32().ShouldBe(404);
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();

        // A routing miss is not a server fault; it must not be labelled as one.
        problem.GetProperty("errorCode").GetString().ShouldBe("resource.not_found");
    }
}
