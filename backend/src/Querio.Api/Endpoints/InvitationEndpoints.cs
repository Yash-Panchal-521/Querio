using Mediator;
using Querio.Api.Common.Endpoints;
using Querio.Api.Common.RateLimiting;
using Querio.Application.Invitations.AcceptInvitation;
using Querio.Application.Invitations.PreviewInvitation;
using Querio.Application.Tenants;
using Querio.Application.Tenants.Invitations;

namespace Querio.Api.Endpoints;

/// <summary>
/// Deliberately not under /tenants/{tenantId}. Someone redeeming an invitation is not a
/// member yet, so the tenant authorization policy would reject them before the handler ran —
/// and the organization is identified by the token, not by the URL.
/// </summary>
internal sealed class InvitationEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/invitations")
            .WithTags("Invitations")
            .RequireAuthorization()
            // Tokens are 256 bits, so guessing is impractical — but throttling means an
            // attempt to try anyway is stopped at the door rather than by arithmetic alone.
            .RequireRateLimiting(RateLimitPolicies.InvitationRedemption);

        // POST for a read, because the input is a credential. A GET would put the token in the
        // path, and our own request logging records RequestPath — so every preview would write
        // a working invitation token into the logs, alongside browser history and Referer.
        group.MapPost("/preview", async (
                PreviewInvitationRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new PreviewInvitationQuery(request.Token), cancellationToken)))
            .WithName("PreviewInvitation")
            .WithSummary("Shows which organization is inviting, and which address it was sent to.")
            .Produces<InvitationPreview>();

        group.MapPost("/accept", async (
                AcceptInvitationRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new AcceptInvitationCommand(request.Token), cancellationToken)))
            .WithName("AcceptInvitation")
            .WithSummary("Joins the organization the invitation was issued for.")
            .WithDescription(
                "Refused unless the signed-in account's email matches the invited address, so a "
                + "forwarded link is useless to anyone else. Accepting twice is harmless.")
            .Produces<TenantSummary>();
    }

    internal sealed record PreviewInvitationRequest(string Token);

    internal sealed record AcceptInvitationRequest(string Token);
}
