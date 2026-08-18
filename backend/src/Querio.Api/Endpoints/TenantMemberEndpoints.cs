using Mediator;
using Querio.Api.Common.Authorization;
using Querio.Api.Common.Endpoints;
using Querio.Application.Tenants.Members;
using Querio.Application.Tenants.Members.ChangeMemberRole;
using Querio.Application.Tenants.Members.LeaveTenant;
using Querio.Application.Tenants.Members.ListMembers;
using Querio.Application.Tenants.Members.RemoveMember;
using Querio.Domain.Tenants;

namespace Querio.Api.Endpoints;

internal sealed class TenantMemberEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup($"/api/v1/tenants/{{{TenantPolicies.TenantRouteKey}:guid}}/members")
            .WithTags("Members")
            .RequireAuthorization();

        group.MapGet("", async (Guid tenantId, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new ListMembersQuery(tenantId), cancellationToken)))
            .WithName("ListMembers")
            .WithSummary("Lists everyone with access to the organization.")
            .RequireAuthorization(TenantPolicies.Member)
            .Produces<IReadOnlyList<MemberSummary>>();

        // Declared before the {userId} route so "me" is never parsed as an identifier.
        group.MapDelete("/me", async (Guid tenantId, IMediator mediator, CancellationToken cancellationToken) =>
            {
                await mediator.Send(new LeaveTenantCommand(tenantId), cancellationToken);

                return TypedResults.NoContent();
            })
            .WithName("LeaveTenant")
            .WithSummary("Leaves the organization. Refused for the last remaining owner.")
            .RequireAuthorization(TenantPolicies.Member)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPatch("/{userId:guid}", async (
                Guid tenantId,
                Guid userId,
                ChangeRoleRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await mediator.Send(new ChangeMemberRoleCommand(tenantId, userId, request.Role), cancellationToken);

                return TypedResults.NoContent();
            })
            .WithName("ChangeMemberRole")
            .WithSummary("Changes a member's role. Owners only; the last owner cannot be demoted.")
            .RequireAuthorization(TenantPolicies.Owner)
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{userId:guid}", async (
                Guid tenantId,
                Guid userId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await mediator.Send(new RemoveMemberCommand(tenantId, userId), cancellationToken);

                return TypedResults.NoContent();
            })
            .WithName("RemoveMember")
            .WithSummary("Removes a member. Admins can remove members only, never each other.")
            .RequireAuthorization(TenantPolicies.Admin)
            .Produces(StatusCodes.Status204NoContent);
    }

    internal sealed record ChangeRoleRequest(TenantRole Role);
}
