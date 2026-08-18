using Mediator;
using Querio.Api.Common.Authorization;
using Querio.Api.Common.Endpoints;
using Querio.Application.Tenants;
using Querio.Application.Tenants.CreateTenant;
using Querio.Application.Tenants.DeleteTenant;
using Querio.Application.Tenants.GetTenant;
using Querio.Application.Tenants.RenameTenant;

namespace Querio.Api.Endpoints;

internal sealed class TenantEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/tenants")
            .WithTags("Organizations")
            .RequireAuthorization();

        // Creation is the one action with no organization to be a member of yet, so it is
        // authenticated but not tenant-scoped.
        group.MapPost("", async (
                CreateTenantRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var summary = await mediator.Send(new CreateTenantCommand(request.Name), cancellationToken);

                return TypedResults.Created($"/api/v1/tenants/{summary.Id}", summary);
            })
            .WithName("CreateTenant")
            .WithSummary("Creates an organization with the caller as its owner.")
            .Produces<TenantSummary>(StatusCodes.Status201Created);

        group.MapGet($"/{{{TenantPolicies.TenantRouteKey}:guid}}", async (
                Guid tenantId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new GetTenantQuery(tenantId), cancellationToken)))
            .WithName("GetTenant")
            .WithSummary("Returns an organization the caller belongs to.")
            .RequireAuthorization(TenantPolicies.Member)
            .Produces<TenantSummary>();

        group.MapPatch($"/{{{TenantPolicies.TenantRouteKey}:guid}}", async (
                Guid tenantId,
                RenameTenantRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new RenameTenantCommand(tenantId, request.Name), cancellationToken)))
            .WithName("RenameTenant")
            .WithSummary("Renames an organization. The slug is left unchanged so shared links keep working.")
            .RequireAuthorization(TenantPolicies.Owner)
            .Produces<TenantSummary>();

        group.MapDelete($"/{{{TenantPolicies.TenantRouteKey}:guid}}", async (
                Guid tenantId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await mediator.Send(new DeleteTenantCommand(tenantId), cancellationToken);

                return TypedResults.NoContent();
            })
            .WithName("DeleteTenant")
            .WithSummary("Permanently deletes an organization and everything in it.")
            .RequireAuthorization(TenantPolicies.Owner)
            .Produces(StatusCodes.Status204NoContent);
    }

    internal sealed record CreateTenantRequest(string Name);

    internal sealed record RenameTenantRequest(string Name);
}
