using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Querio.Api.Infrastructure.ExceptionHandling;
using Querio.Domain.Common.Errors;

namespace Querio.Api.Tests.Infrastructure;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Known_exception_maps_to_its_own_status_and_error_code()
    {
        var context = CreateContext();
        var handler = CreateHandler(environmentName: Environments.Production);

        var handled = await handler.TryHandleAsync(
            context,
            new NotFoundException("Document", "doc_42"),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);

        var problem = await ReadProblemDetailsAsync(context);
        problem.GetProperty("errorCode").GetString().ShouldBe("resource.not_found");
        problem.GetProperty("detail").GetString().ShouldBe("Document 'doc_42' was not found.");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Validation_exception_surfaces_field_errors()
    {
        var context = CreateContext();
        var handler = CreateHandler(environmentName: Environments.Production);

        var handled = await handler.TryHandleAsync(
            context,
            new ValidationException("fileName", "File name is required."),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        var problem = await ReadProblemDetailsAsync(context);
        problem.GetProperty("errorCode").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors")
            .GetProperty("fileName")[0]
            .GetString()
            .ShouldBe("File name is required.");
    }

    [Fact]
    public async Task Unexpected_exception_hides_internals_outside_development()
    {
        var context = CreateContext();
        var handler = CreateHandler(environmentName: Environments.Production);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("connection string 'Host=secret-db' is invalid"),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        var problem = await ReadProblemDetailsAsync(context);
        problem.GetProperty("errorCode").GetString().ShouldBe("server.unexpected_error");

        var detail = problem.GetProperty("detail").GetString();
        detail.ShouldNotBeNull();
        detail.ShouldNotContain("secret-db");
    }

    [Fact]
    public async Task Unexpected_exception_keeps_details_in_development()
    {
        var context = CreateContext();
        var handler = CreateHandler(environmentName: Environments.Development);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("pgvector extension is not installed"),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();

        var problem = await ReadProblemDetailsAsync(context);

        var detail = problem.GetProperty("detail").GetString();
        detail.ShouldNotBeNull();
        detail.ShouldContain("pgvector extension is not installed");
    }

    [Fact]
    public async Task Client_cancellation_is_swallowed_without_writing_a_response()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        var context = CreateContext();
        context.RequestAborted = aborted.Token;

        var handler = CreateHandler(environmentName: Environments.Production);

        var handled = await handler.TryHandleAsync(
            context,
            new OperationCanceledException(),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.Body.Length.ShouldBe(0);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = BuildServiceProvider(),
        };

        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/documents/doc_42";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static GlobalExceptionHandler CreateHandler(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var problemDetailsService = BuildServiceProvider().GetRequiredService<IProblemDetailsService>();

        return new GlobalExceptionHandler(
            problemDetailsService,
            environment,
            NullLogger<GlobalExceptionHandler>.Instance);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);

        return services.BuildServiceProvider();
    }

    private static async Task<JsonElement> ReadProblemDetailsAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        return document.RootElement.Clone();
    }
}
