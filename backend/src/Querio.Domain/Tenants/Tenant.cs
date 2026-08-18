using Querio.Domain.Common;
using Querio.Domain.Common.Errors;

namespace Querio.Domain.Tenants;

/// <summary>
/// An organization: the unit everything else in Querio is scoped to.
///
/// Memberships are held inside the aggregate rather than manipulated freely, because the
/// rule "an organization always has at least one Owner" cannot be enforced from outside. An
/// organization with no Owner is unadministrable and unrecoverable, so the invariant lives
/// where it cannot be bypassed.
/// </summary>
public sealed class Tenant : Entity, IAuditable
{
    private readonly List<Membership> memberships = [];

    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    private Tenant(string name, string slug, Guid createdByUserId)
    {
        Name = name;
        Slug = slug;
        CreatedByUserId = createdByUserId;
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public IReadOnlyCollection<Membership> Memberships => memberships.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Creates the organization together with its first Owner, so it is never persisted in a
    /// state where nobody can administer it.
    /// </summary>
    public static Tenant Create(string name, string slug, Guid ownerUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var tenant = new Tenant(name.Trim(), slug, ownerUserId);

        tenant.memberships.Add(Membership.Create(tenant.Id, ownerUserId, TenantRole.Owner));

        return tenant;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // The slug is deliberately left alone: it is in URLs people have already shared.
        Name = name.Trim();
    }

    public Membership? MembershipFor(Guid userId) =>
        memberships.SingleOrDefault(membership => membership.UserId == userId);

    public int OwnerCount => memberships.Count(membership => membership.Role == TenantRole.Owner);

    /// <summary>
    /// Adds a member. Rejects a second membership for the same person rather than silently
    /// granting two roles at once.
    /// </summary>
    public Membership AddMember(Guid userId, TenantRole role)
    {
        if (MembershipFor(userId) is not null)
        {
            throw new ConflictException("That person is already a member of this organization.", "membership.already_exists");
        }

        var membership = Membership.Create(Id, userId, role);

        memberships.Add(membership);

        return membership;
    }

    public void ChangeRole(Guid userId, TenantRole role)
    {
        var membership = MembershipFor(userId)
            ?? throw new NotFoundException("Member", userId);

        if (membership.Role == role)
        {
            return;
        }

        GuardLastOwner(membership, role);

        membership.ChangeRole(role);
    }

    public void RemoveMember(Guid userId)
    {
        var membership = MembershipFor(userId)
            ?? throw new NotFoundException("Member", userId);

        GuardLastOwner(membership, replacementRole: null);

        memberships.Remove(membership);
    }

    /// <summary>
    /// Blocks the one transition that cannot be undone from inside the product: removing or
    /// demoting the final Owner, which would leave the organization with nobody able to
    /// invite, promote, or delete it.
    /// </summary>
    private void GuardLastOwner(Membership membership, TenantRole? replacementRole)
    {
        var losingAnOwner = membership.Role == TenantRole.Owner && replacementRole != TenantRole.Owner;

        if (losingAnOwner && OwnerCount <= 1)
        {
            throw new ConflictException(
                "This is the organization's only owner. Promote another owner first.",
                "tenant.last_owner");
        }
    }
}
