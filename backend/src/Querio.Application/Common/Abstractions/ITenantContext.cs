namespace Querio.Application.Common.Abstractions;

/// <summary>
/// The organization the current request is acting in, established by the authorization layer
/// once membership has been proven against the database — never taken from a header or a
/// token claim the caller controls.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant => TenantId is not null;
}
