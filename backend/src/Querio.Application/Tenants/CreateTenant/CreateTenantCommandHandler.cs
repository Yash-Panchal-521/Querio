using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Extensions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Tenants;
using Querio.Domain.Users;

namespace Querio.Application.Tenants.CreateTenant;

internal sealed class CreateTenantCommandHandler(
    IQuerioDbContext dbContext,
    ICurrentUser currentUser) : ICommandHandler<CreateTenantCommand, TenantSummary>
{
    /// <summary>
    /// Bounded rather than looping forever: if this many slugs are taken the name is so
    /// generic that another suffix is not the answer, and an unbounded loop under contention
    /// would hold a request open indefinitely.
    /// </summary>
    private const int MaxSlugAttempts = 25;

    public async ValueTask<TenantSummary> Handle(
        CreateTenantCommand command,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.RequireProvisionedUserAsync(dbContext, cancellationToken);

        // Gated because invitations are matched by email address: creating an organization
        // under an unproven address would make invitations to it trustworthy on the
        // creator's say-so alone.
        if (!user.EmailVerified)
        {
            throw new EmailNotVerifiedException();
        }

        var slug = await ReserveSlugAsync(TenantSlug.From(command.Name), cancellationToken);
        var tenant = Tenant.Create(command.Name, slug, user.Id);

        dbContext.Tenants.Add(tenant);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TenantSummary(tenant.Id, tenant.Name, tenant.Slug, TenantRole.Owner, MemberCount: 1);
    }

    private async Task<string> ReserveSlugAsync(string preferred, CancellationToken cancellationToken)
    {
        var taken = await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Slug == preferred || tenant.Slug.StartsWith(preferred + "-"))
            .Select(tenant => tenant.Slug)
            .ToListAsync(cancellationToken);

        if (!taken.Contains(preferred, StringComparer.Ordinal))
        {
            return preferred;
        }

        for (var suffix = 2; suffix <= MaxSlugAttempts; suffix++)
        {
            var candidate = TenantSlug.WithSuffix(preferred, suffix);

            if (!taken.Contains(candidate, StringComparer.Ordinal))
            {
                return candidate;
            }
        }

        throw new ConflictException(
            "Too many organizations share this name. Choose a more distinctive one.",
            "tenant.slug_exhausted");
    }
}
