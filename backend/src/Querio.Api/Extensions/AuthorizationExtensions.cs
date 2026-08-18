using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Querio.Api.Common.Authorization;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Tenants;

namespace Querio.Api.Extensions;

internal static class AuthorizationExtensions
{
    public static WebApplicationBuilder AddQuerioAuthorization(this WebApplicationBuilder builder)
    {
        builder.Services.AddMemoryCache();

        builder.Services.AddScoped<TenantContext>();
        builder.Services.AddScoped<ITenantContext>(serviceProvider =>
            serviceProvider.GetRequiredService<TenantContext>());

        builder.Services.AddScoped<IAuthorizationHandler, TenantAuthorizationHandler>();

        // Replaces the default 403 with 404 when the caller is not a member at all.
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TenantAwareAuthorizationResultHandler>();

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TenantPolicies.Member, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new TenantRoleRequirement(TenantRole.Member)))
            .AddPolicy(TenantPolicies.Admin, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new TenantRoleRequirement(TenantRole.Admin)))
            .AddPolicy(TenantPolicies.Owner, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new TenantRoleRequirement(TenantRole.Owner)));

        return builder;
    }
}
