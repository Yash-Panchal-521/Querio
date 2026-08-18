using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Extensions;
using Querio.Application.Tenants;
using Querio.Domain.Common.Errors;
using Querio.Domain.Tenants;

namespace Querio.Application.Invitations.AcceptInvitation;

internal sealed class AcceptInvitationCommandHandler(
    IQuerioDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<AcceptInvitationCommand, TenantSummary>
{
    public async ValueTask<TenantSummary> Handle(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.RequireProvisionedUserAsync(dbContext, cancellationToken);
        var tokenHash = InvitationToken.Hash(command.Token);
        var now = timeProvider.GetUtcNow();

        // IgnoreQueryFilters, deliberately: joining is precisely the act of gaining access to
        // an organization the caller is not yet in, so no tenant is established.
        var invitation = await dbContext.Invitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException("This invitation link is not valid.");

        var tenant = await dbContext.Tenants
            .Include(candidate => candidate.Memberships)
            .FirstOrDefaultAsync(candidate => candidate.Id == invitation.TenantId, cancellationToken)
            ?? throw new NotFoundException("Organization", invitation.TenantId);

        // Double-clicking a link, or opening it twice, should land the person in the
        // organization rather than showing them an error about something that already worked.
        var existing = tenant.MembershipFor(user.Id);

        if (existing is not null)
        {
            return Summarise(tenant, existing.Role);
        }

        // Verifies expiry, revocation, prior use, and that the address matches.
        invitation.Accept(user.Id, user.Email, now);

        tenant.AddMember(user.Id, invitation.Role);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Summarise(tenant, invitation.Role);
    }

    private static TenantSummary Summarise(Tenant tenant, TenantRole role) =>
        new(tenant.Id, tenant.Name, tenant.Slug, role, tenant.Memberships.Count);
}
