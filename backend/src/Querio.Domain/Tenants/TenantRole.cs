namespace Querio.Domain.Tenants;

/// <summary>
/// Roles within one organization.
///
/// Values are ordered and spaced deliberately: authorization asks "is this role at least
/// Admin", which is a comparison, not a set membership test. The gaps leave room to insert a
/// role (Viewer below Member, say) without renumbering stored rows.
/// </summary>
public enum TenantRole
{
    Member = 10,
    Admin = 20,
    Owner = 30,
}
