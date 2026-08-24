using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Querio.Api.Common.Authentication;
using Querio.Application.Common.Abstractions;

namespace Querio.Api.Common.Authorization;

/// <summary>
/// Resolves the organization from the route and proves membership against the database.
///
/// Membership is never read from a token claim: Firebase claims cap at roughly a kilobyte
/// and only refresh when the client asks, so a removed member would keep working from a
/// cached token for up to an hour.
/// </summary>
internal sealed class TenantAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IQuerioDbContext dbContext,
    ICurrentUser currentUser,
    ITenantScope tenantScope,
    IMemoryCache cache) : AuthorizationHandler<TenantRoleRequirement>
{
    /// <summary>
    /// Bounds how long a removed member keeps access. Short enough to honour "access ends
    /// within 30 seconds", long enough that a burst of requests costs one query, not dozens.
    /// </summary>
    private static readonly TimeSpan MembershipCacheLifetime = TimeSpan.FromSeconds(30);

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null || currentUser.FirebaseUid is null)
        {
            context.Fail(Reason(TenantAuthorizationFailures.NotAMember));

            return;
        }

        if (!TryReadTenantId(httpContext, out var tenantId))
        {
            // A malformed id cannot identify an organization the caller belongs to, so it is
            // indistinguishable from one that does not exist.
            context.Fail(Reason(TenantAuthorizationFailures.NotAMember));

            return;
        }

        var membership = await ResolveMembershipAsync(currentUser.FirebaseUid, tenantId, httpContext.RequestAborted);

        if (membership is null)
        {
            context.Fail(Reason(TenantAuthorizationFailures.NotAMember));

            return;
        }

        if (membership.Role < requirement.MinimumRole)
        {
            // The caller is a member, so the organization's existence is already known to
            // them. Refusing with 403 here leaks nothing and is the honest answer.
            context.Fail(Reason(TenantAuthorizationFailures.InsufficientRole));

            return;
        }

        tenantScope.Establish(tenantId);

        context.Succeed(requirement);
    }

    private AuthorizationFailureReason Reason(string code) => new(this, code);

    private static bool TryReadTenantId(HttpContext httpContext, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        return httpContext.Request.RouteValues.TryGetValue(TenantPolicies.TenantRouteKey, out var raw)
            && Guid.TryParse(raw?.ToString(), out tenantId);
    }

    private async Task<MembershipSnapshot?> ResolveMembershipAsync(
        string firebaseUid,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"membership:{firebaseUid}:{tenantId}";

        if (cache.TryGetValue<MembershipSnapshot?>(cacheKey, out var cached))
        {
            return cached;
        }

        var snapshot = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId)
            .Join(
                dbContext.Users.AsNoTracking().Where(user => user.FirebaseUid == firebaseUid),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new MembershipSnapshot(user.Id, membership.Role))
            .FirstOrDefaultAsync(cancellationToken);

        // Negative results are cached too, so a probe loop cannot turn into a query per
        // request. The same 30-second bound applies.
        cache.Set(cacheKey, snapshot, MembershipCacheLifetime);

        return snapshot;
    }

    private sealed record MembershipSnapshot(Guid UserId, Domain.Tenants.TenantRole Role);
}
