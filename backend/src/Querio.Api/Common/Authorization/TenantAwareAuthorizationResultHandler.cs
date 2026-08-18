using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Querio.Domain.Common.Errors;

namespace Querio.Api.Common.Authorization;

/// <summary>
/// Turns "you are not a member of this organization" into 404 rather than 403.
///
/// A 403 confirms the resource exists, which would let anyone with an account discover
/// whether a given organization is a Querio customer by probing identifiers. A member whose
/// role is merely too low still gets 403 — they already know the organization exists, so
/// there is nothing left to conceal and the honest answer is more useful.
/// </summary>
internal sealed class TenantAwareAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && FailedBecauseNotAMember(authorizeResult))
        {
            // Thrown rather than written directly so it flows through the global handler and
            // comes out as ProblemDetails with a traceId, exactly like every other 404.
            throw new NotFoundException(
                "Organization",
                context.Request.RouteValues.TryGetValue(TenantPolicies.TenantRouteKey, out var tenantId)
                    ? tenantId?.ToString() ?? "unknown"
                    : "unknown");
        }

        return defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private static bool FailedBecauseNotAMember(PolicyAuthorizationResult authorizeResult) =>
        authorizeResult.AuthorizationFailure?.FailureReasons
            .Any(reason => reason.Message == TenantAuthorizationFailures.NotAMember)
        ?? false;
}
