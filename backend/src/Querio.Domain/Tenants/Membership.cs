using Querio.Domain.Common;

namespace Querio.Domain.Tenants;

/// <summary>
/// One person's access to one organization. This is the authoritative answer to "what may
/// they do here" — never a token claim, which would go stale for up to an hour.
///
/// Deliberately not tenant-filtered: this is the table that <em>decides</em> tenant access,
/// and listing someone's organizations is inherently a cross-tenant question.
/// </summary>
public sealed class Membership : Entity, IAuditable
{
    private Membership()
    {
    }

    private Membership(Guid tenantId, Guid userId, TenantRole role)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    internal static Membership Create(Guid tenantId, Guid userId, TenantRole role) =>
        new(tenantId, userId, role);

    internal void ChangeRole(TenantRole role) => Role = role;
}
