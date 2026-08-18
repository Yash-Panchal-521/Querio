using Mediator;
using Querio.Api.Common.Endpoints;
using Querio.Api.Common.RateLimiting;
using Querio.Application.Users;
using Querio.Application.Users.BootstrapCurrentUser;
using Querio.Application.Users.GetCurrentUser;

namespace Querio.Api.Endpoints;

internal sealed class MeEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/me")
            .WithTags("Me")
            .RequireAuthorization();

        group.MapPost("/bootstrap", async (IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new BootstrapCurrentUserCommand(), cancellationToken)))
            .WithName("BootstrapCurrentUser")
            .WithSummary("Creates or refreshes the caller's profile from their token.")
            .WithDescription(
                "Called after every sign-in, not only the first. Safe to repeat: it upserts, and "
                + "refreshes the stored email, verification state and display name from the token.")
            .RequireRateLimiting(RateLimitPolicies.Bootstrap)
            .Produces<UserProfile>();

        group.MapGet("", async (IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new GetCurrentUserQuery(), cancellationToken)))
            .WithName("GetCurrentUser")
            .WithSummary("Returns the caller's profile.")
            .WithDescription("Responds with error code user.not_provisioned if bootstrap has not run yet.")
            .Produces<UserProfile>();
    }
}
