using Mediator;
using Querio.Api.Common.Authorization;
using Querio.Api.Common.Endpoints;
using Querio.Application.Tenants.Invitations;
using Querio.Application.Tenants.Invitations.InviteMember;
using Querio.Application.Tenants.Invitations.ListInvitations;
using Querio.Application.Tenants.Invitations.RevokeInvitation;
using Querio.Domain.Tenants;

namespace Querio.Api.Endpoints;

internal sealed class TenantInvitationEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup($"/api/v1/tenants/{{{TenantPolicies.TenantRouteKey}:guid}}/invitations")
            .WithTags("Invitations")
            .RequireAuthorization(TenantPolicies.Admin);

        group.MapPost("", async (
                Guid tenantId,
                InviteMemberRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var invitation = await mediator.Send(
                    new InviteMemberCommand(tenantId, request.Email, request.Role),
                    cancellationToken);

                return TypedResults.Created($"/api/v1/tenants/{tenantId}/invitations/{invitation.Id}", invitation);
            })
            .WithName("InviteMember")
            .WithSummary("Issues a single-use invitation bound to the given email address.")
            .WithDescription(
                "The response carries the only copy of the token that will ever exist — the database "
                + "stores a hash. Hand it to the inviter immediately; it cannot be retrieved again.")
            .Produces<IssuedInvitation>(StatusCodes.Status201Created);

        group.MapGet("", async (Guid tenantId, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new ListInvitationsQuery(tenantId), cancellationToken)))
            .WithName("ListInvitations")
            .WithSummary("Lists invitations that are still open.")
            .Produces<IReadOnlyList<InvitationSummary>>();

        group.MapDelete("/{invitationId:guid}", async (
                Guid tenantId,
                Guid invitationId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await mediator.Send(new RevokeInvitationCommand(tenantId, invitationId), cancellationToken);

                return TypedResults.NoContent();
            })
            .WithName("RevokeInvitation")
            .WithSummary("Revokes an invitation. The link stops working immediately, not at expiry.")
            .Produces(StatusCodes.Status204NoContent);
    }

    internal sealed record InviteMemberRequest(string Email, TenantRole Role);
}
