namespace Querio.Domain.Common;

/// <summary>
/// Marks a row as belonging to exactly one organization. Everything implementing this is
/// filtered at the data layer, so a handler that forgets a WHERE clause still cannot read
/// another organization's rows.
/// </summary>
public interface IHasTenant
{
    Guid TenantId { get; }
}
